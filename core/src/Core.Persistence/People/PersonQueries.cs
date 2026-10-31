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
