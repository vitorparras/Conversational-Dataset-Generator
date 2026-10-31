using Core.Persistence.Custody;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.People;

/// <summary>The commands every operation over a person shares: one statement bound and run, the state of a person read, and the reason an actor is refused.</summary>
internal static class PersonQueries
{
    internal static void Add(NpgsqlCommand command, string name, NpgsqlDbType type, object? value) =>
        _ = command.Parameters.AddWithValue(name, type, value ?? DBNull.Value);

    internal static async Task<long?> WriteAsync(this NpgsqlDataSource dataSource, string sql, Action<NpgsqlCommand> bind, CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(sql);
        await using (command.ConfigureAwait(false))
        {
            bind(command);
            return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as long?;
        }
    }

    /// <summary>
    /// Runs the statement and adds the custody event of that change in the same serializable transaction; null when the statement wrote nothing.
    /// For a person who exists the event goes first, so that someone who deactivates themselves is judged as they were when they acted.
    /// The author is a person or the system, said so by the caller and never deduced from a missing actor.
    /// </summary>
    internal static async Task<PersonResult?> WriteChainedAsync(this NpgsqlDataSource dataSource, PersonStatement statement, CustodyAuthor author, CancellationToken cancellationToken)
    {
        if (statement.PersonId is not { } personId)
        {
            return await CreateChainedAsync(dataSource, statement, author, cancellationToken).ConfigureAwait(false);
        }

        var pending = CustodyEvent.Of(author, new CustodyItem(CustodyItemKind.Person, personId), statement.Step, CustodyReason.AdministrativeAct);
        var result = await CustodyTransaction.AppendBeforeAsync(
            dataSource,
            pending,
            async scope => await ScalarAsync(scope, statement, cancellationToken).ConfigureAwait(false) is not null,
            cancellationToken).ConfigureAwait(false);
        return result is null ? null : PersonRefusals.Chained(result, personId);
    }

    private static async Task<PersonResult?> CreateChainedAsync(NpgsqlDataSource dataSource, PersonStatement statement, CustodyAuthor author, CancellationToken cancellationToken)
    {
        long created = 0;
        var result = await CustodyTransaction.AppendAfterAsync(
            dataSource,
            async scope =>
            {
                created = await ScalarAsync(scope, statement, cancellationToken).ConfigureAwait(false) ?? 0;
                return created == 0 ? null : CustodyEvent.Of(author, new CustodyItem(CustodyItemKind.Person, created), statement.Step, CustodyReason.AdministrativeAct);
            },
            cancellationToken).ConfigureAwait(false);
        return result is null ? null : PersonRefusals.Chained(result, created);
    }

    private static async Task<long?> ScalarAsync(CustodyScope scope, PersonStatement statement, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(statement.Sql);
        await using (command.ConfigureAwait(false))
        {
            statement.Bind(command);
            return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as long?;
        }
    }

    internal static async Task<bool?> ReadActiveAsync(this NpgsqlDataSource dataSource, long personId, CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(PersonStatements.IsActive);
        await using (command.ConfigureAwait(false))
        {
            Add(command, "id", NpgsqlDbType.Bigint, personId);
            return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as bool?;
        }
    }

    /// <summary>The refusal that fits an actor who is not registered or was deactivated; null when nobody acts as a person or the actor is active.</summary>
    internal static async Task<PersonResult?> ActorRefusalAsync(this NpgsqlDataSource dataSource, long? actorId, CancellationToken cancellationToken)
    {
        if (actorId is not { } actor)
        {
            return null;
        }

        return await dataSource.ReadActiveAsync(actor, cancellationToken).ConfigureAwait(false) switch
        {
            true => null,
            false => PersonRefusals.ActorDeactivated(),
            null => PersonRefusals.ActorUnknown(),
        };
    }
}
