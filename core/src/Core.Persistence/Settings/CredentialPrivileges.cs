using Npgsql;

namespace Core.Persistence.Settings;

/// <summary>What the credential of a connection may do to the database: the applier of the configuration reads and nothing else (decision 0053), and it asks before it applies anything.</summary>
public static class CredentialPrivileges
{
    private const string Writes = """
        SELECT EXISTS (
            SELECT 1 FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public' AND c.relkind IN ('r', 'p')
              AND (pg_catalog.has_table_privilege(c.oid, 'INSERT') OR pg_catalog.has_table_privilege(c.oid, 'UPDATE')
                   OR pg_catalog.has_table_privilege(c.oid, 'DELETE') OR pg_catalog.has_table_privilege(c.oid, 'TRUNCATE')))
            OR pg_catalog.has_schema_privilege('public', 'CREATE')
        """;

    /// <summary>Whether the credential can insert, update, delete or truncate in a table of the application, or create in its schema.</summary>
    /// <param name="dataSource">The source of the connections whose credential is asked about.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public static async Task<bool> CanWriteAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        var command = dataSource.CreateCommand(Writes);
        await using (command.ConfigureAwait(false))
        {
            return (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
        }
    }
}
