using System.Data;
using System.Globalization;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Custody.Chain.Append;
using Core.Persistence.People;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Content.Rerun;

/// <summary>What the queue and the rerun share: statements bound by name, rows read whole, the events of concluded items, and a hold given back or spent.</summary>
internal static class RerunRows
{
    /// <summary>The name the queue keeps for the refusal, and back; the database accepts no other.</summary>
    internal static string NameOf(RerunRefusal refusal) => refusal switch
    {
        RerunRefusal.Emptied => "emptied",
        RerunRefusal.KeyLost => "key_lost",
        RerunRefusal.Failed => "failed",
        RerunRefusal.Undecided => "undecided",
        RerunRefusal.PseudonymWithoutPair => "pseudonym_without_pair",
        _ => throw new ArgumentOutOfRangeException(nameof(refusal)),
    };

    internal static RerunRefusal RefusalOf(string name) => name switch
    {
        "emptied" => RerunRefusal.Emptied,
        "key_lost" => RerunRefusal.KeyLost,
        "failed" => RerunRefusal.Failed,
        "undecided" => RerunRefusal.Undecided,
        "pseudonym_without_pair" => RerunRefusal.PseudonymWithoutPair,
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    /// <summary>The version, the moment, the end of a new hold and the tries, for the statements over a version.</summary>
    internal static Bound[] Over(string version, DateTimeOffset now) =>
        [new("version", NpgsqlDbType.Text, version), new("now", NpgsqlDbType.TimestampTz, now), new("until", NpgsqlDbType.TimestampTz, now + SanitizationReruns.Hold),
            new("tries", NpgsqlDbType.Integer, SanitizationReruns.Tries)];

    /// <summary>The entry and the end of the hold, which match only while the hold is this one, the moment and the tries.</summary>
    internal static Bound[] Of(RerunHold hold, DateTimeOffset now) =>
        [new("entry", NpgsqlDbType.Bigint, hold.Entry), new("held", NpgsqlDbType.TimestampTz, hold.Until), new("now", NpgsqlDbType.TimestampTz, now),
            new("tries", NpgsqlDbType.Integer, SanitizationReruns.Tries)];

    /// <summary>Declares the domain in a transaction the chain opened.</summary>
    internal static Task DeclareAsync(CustodyScope scope, int domainId, CancellationToken cancellationToken) =>
        ScalarAsync(scope, (ContentTransaction.Declare, [new("domain", NpgsqlDbType.Text, domainId.ToString(CultureInfo.InvariantCulture))]), cancellationToken);

    internal static async Task<object?> ScalarAsync(CustodyScope scope, (string Statement, Bound[] Values) query, CancellationToken cancellationToken)
    {
        var command = Command(scope, query);
        await using (command.ConfigureAwait(false))
        {
            return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Every row the statement answers, each read by the function given, read whole before anything else runs on the connection.</summary>
    internal static async Task<List<T>> RowsAsync<T>(CustodyScope scope, (string Statement, Bound[] Values) query, Func<NpgsqlDataReader, T> read, CancellationToken cancellationToken)
    {
        var command = Command(scope, query);
        await using var commandScope = command.ConfigureAwait(false);
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using var readerScope = reader.ConfigureAwait(false);
        var rows = new List<T>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(read(reader));
        }

        return rows;
    }

    /// <summary>Records each item a statement concluded, with the result and its version; the last result, or none when it concluded none.</summary>
    internal static async Task<CustodyResult?> RecordEachAsync(CustodyScope scope, IEnumerable<(long Item, string Version)> concluded, CustodyReason reason, CancellationToken cancellationToken)
    {
        CustodyResult? last = null;
        foreach (var (item, version) in concluded)
        {
            last = await CustodyAppend.AppendAsync(scope, CustodyEvent.ItemResanitized(item, reason, version), cancellationToken).ConfigureAwait(false);
            if (last.Outcome != CustodyOutcome.Appended)
            {
                return last;
            }
        }

        return last;
    }

    /// <summary>Gives the held item back, or spends a try of it, by the statement given; at the last try it is refused, with its event.</summary>
    internal static async Task<RerunOutcome> SpendAsync(
        NpgsqlDataSource dataSource, RerunHold hold, (string Statement, RerunOutcome Back, DateTimeOffset Now) spent, CancellationToken cancellationToken)
    {
        var transaction = await ContentTransaction.BeginAsync(dataSource, hold.DomainId, IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var scope = transaction.AsCustodyScope();
            var state = await ScalarAsync(scope, (spent.Statement, Of(hold, spent.Now)), cancellationToken).ConfigureAwait(false) as string;
            var refused = state is "done";
            if (state is null || (refused && await RecordEachAsync(scope, [(hold.ItemId, hold.Version)], CustodyReason.SanitizationRefused, cancellationToken).ConfigureAwait(false)
                is { Outcome: not CustodyOutcome.Appended }))
            {
                return state is null ? RerunOutcome.NotHeld : RerunOutcome.NotRecorded;
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return refused ? RerunOutcome.Refused : spent.Back;
        }
    }

    private static NpgsqlCommand Command(CustodyScope scope, (string Statement, Bound[] Values) query)
    {
        var command = scope.CreateCommand(query.Statement);
        foreach (var bound in query.Values.Where(bound => query.Statement.Contains("@" + bound.Name, StringComparison.Ordinal)))
        {
            PersonQueries.Add(command, bound.Name, bound.Type, bound.Value);
        }

        return command;
    }

    /// <summary>A value bound to a statement only when the statement names it.</summary>
    internal readonly record struct Bound(string Name, NpgsqlDbType Type, object? Value);
}
