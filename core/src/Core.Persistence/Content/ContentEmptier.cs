using System.Globalization;
using Core.Persistence.Custody;
using Core.Persistence.People;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Content;

/// <summary>
/// The one way an item is emptied: in one serializable transaction the chain records the emptying or its refusal, and only if the event of the emptying entered are the item
/// and its maps cleared, with the marker set. Only a person who holds the function of curation of the collection empties; the domain is the one the row policy shows, so an item
/// of another domain is not found. A refusal to empty is an event too, and the content stays as it was.
/// </summary>
public sealed class ContentEmptier
{
    private const string FunctionOfPerson = "SELECT function_name FROM public.person WHERE id = @id";

    private readonly NpgsqlDataSource dataSource;

    /// <summary>The emptier writes through the data source built with the application's credential.</summary>
    public ContentEmptier(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    /// <summary>Empties the item, or records why it was not emptied.</summary>
    /// <param name="request">Who asks, which item of which domain, why, and the protocol that asked.</param>
    /// <param name="cancellationToken">Cancels the emptying.</param>
    public Task<EmptyingResult> EmptyAsync(ItemEmptying request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.DomainId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.ItemId);
        return ProtocolReference.IsValid(request.Reference)
            ? EmptyingAsync(request, cancellationToken)
            : Task.FromResult(new EmptyingResult { Outcome = EmptyingOutcome.ReferenceRefused });
    }

    private static CustodyEvent EventOf(ItemEmptying request, EmptyingOutcome outcome)
    {
        if (outcome == EmptyingOutcome.Emptied)
        {
            return CustodyEvent.Emptied(request.Author, request.ItemId, ReasonOf(request.Motive), request.Reference);
        }

        var refusal = outcome == EmptyingOutcome.AuthorWithoutFunction ? CustodyReason.AuthorWithoutFunction : CustodyReason.ItemAlreadyEmptied;
        return CustodyEvent.EmptyingRefused(request.Author, request.ItemId, refusal, request.Reference);
    }

    private static CustodyReason ReasonOf(EmptyingMotive motive) => motive switch
    {
        EmptyingMotive.RemovalRequestedBySubject => CustodyReason.RemovalRequestedBySubject,
        EmptyingMotive.ContractRequirement => CustodyReason.ContractRequirement,
        _ => throw new ArgumentOutOfRangeException(nameof(motive), motive, "The motive is not one an emptying may give."),
    };

    private static async Task DeclareDomainAsync(CustodyScope scope, int domainId, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(ContentTransaction.Declare);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.AddWithValue("domain", domainId.ToString(CultureInfo.InvariantCulture));
            _ = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<object?> ScalarAsync(CustodyScope scope, string statement, long id, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(statement);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = id });
            return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task ClearAsync(CustodyScope scope, long itemId, CancellationToken cancellationToken)
    {
        _ = await ScalarAsync(scope, ContentQueries.EmptyItemText, itemId, cancellationToken).ConfigureAwait(false);
        var maps = scope.CreateCommand(ContentQueries.EmptyReversalMaps);
        await using (maps.ConfigureAwait(false))
        {
            _ = maps.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = itemId });
            _ = await maps.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<EmptyingResult> EmptyingAsync(ItemEmptying request, CancellationToken cancellationToken)
    {
        var judgment = new Judgment();
        var recorded = await CustodyTransaction.RunAsync(dataSource, scope => EmptyInAsync(scope, request, judgment, cancellationToken), cancellationToken).ConfigureAwait(false);
        if (judgment.Outcome == EmptyingOutcome.NotFound)
        {
            return new EmptyingResult { Outcome = EmptyingOutcome.NotFound };
        }

        return recorded?.Outcome == CustodyOutcome.Appended
            ? new EmptyingResult { Outcome = judgment.Outcome, Sequence = recorded.Sequence }
            : new EmptyingResult { Outcome = EmptyingOutcome.NotRecorded, Refusal = recorded };
    }

    private static async Task<CustodyResult?> EmptyInAsync(CustodyScope scope, ItemEmptying request, Judgment judgment, CancellationToken cancellationToken)
    {
        await DeclareDomainAsync(scope, request.DomainId, cancellationToken).ConfigureAwait(false);
        judgment.Outcome = await JudgedAsync(scope, request, cancellationToken).ConfigureAwait(false);
        if (judgment.Outcome == EmptyingOutcome.NotFound)
        {
            return null;
        }

        var appended = await CustodyTransaction.AppendAsync(scope, EventOf(request, judgment.Outcome), cancellationToken).ConfigureAwait(false);
        if (appended.Outcome == CustodyOutcome.Appended && judgment.Outcome == EmptyingOutcome.Emptied)
        {
            await ClearAsync(scope, request.ItemId, cancellationToken).ConfigureAwait(false);
        }

        return await CustodyTransaction.CommitIfAppendedAsync(scope, appended, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The function comes first, so that an attempt without it is recorded whatever the item is; the item is looked for only for someone who may empty.</summary>
    private static async Task<EmptyingOutcome> JudgedAsync(CustodyScope scope, ItemEmptying request, CancellationToken cancellationToken)
    {
        if (!await HoldsTheFunctionAsync(scope, request.Author, cancellationToken).ConfigureAwait(false))
        {
            return EmptyingOutcome.AuthorWithoutFunction;
        }

        var alreadyEmptied = await ScalarAsync(scope, ContentQueries.ReadItemEmptied, request.ItemId, cancellationToken).ConfigureAwait(false);
        if (alreadyEmptied is not bool emptied)
        {
            return EmptyingOutcome.NotFound;
        }

        return emptied ? EmptyingOutcome.AlreadyEmptied : EmptyingOutcome.Emptied;
    }

    private static async Task<bool> HoldsTheFunctionAsync(CustodyScope scope, CustodyAuthor author, CancellationToken cancellationToken) =>
        author.PersonId is { } person
        && await ScalarAsync(scope, FunctionOfPerson, person, cancellationToken).ConfigureAwait(false) is string function
        && string.Equals(function, PersonFunctions.Curation, StringComparison.Ordinal);

    private sealed class Judgment
    {
        internal EmptyingOutcome Outcome { get; set; }
    }
}
