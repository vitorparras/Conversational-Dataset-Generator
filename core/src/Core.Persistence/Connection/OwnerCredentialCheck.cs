using Npgsql;

namespace Core.Persistence.Connection;

/// <summary>Tells whether the credential a data source connects with is the owner of the schema, which the application never is: the owner migrates and nothing else.</summary>
public static class OwnerCredentialCheck
{
    private const string Query = """
        SELECT pg_catalog.pg_has_role(c.relowner, 'MEMBER')
        FROM pg_catalog.pg_class c
        WHERE c.oid = pg_catalog.to_regclass('public.person')
        """;

    /// <summary>Whether the role is the owner of the registry of people or a member of the role that owns it; null when the registry does not exist, which is a database not migrated.</summary>
    /// <param name="dataSource">The source of the connections.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public static Task<bool?> HoldsTheSchemaAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        return ReadAsync(dataSource, cancellationToken);
    }

    private static async Task<bool?> ReadAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(Query);
        await using (command.ConfigureAwait(false))
        {
            return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as bool?;
        }
    }
}
