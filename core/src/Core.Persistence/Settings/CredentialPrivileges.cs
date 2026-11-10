using Npgsql;

namespace Core.Persistence.Settings;

/// <summary>What the credential of a connection may do to the database: the applier of the configuration reads and nothing else (decision 0053), and it asks before it applies anything.</summary>
public static class CredentialPrivileges
{
    private const string Writes = """
        SELECT EXISTS (
            SELECT 1 FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname NOT IN ('pg_catalog', 'information_schema') AND left(n.nspname, 3) <> 'pg_' AND c.relkind IN ('r', 'p')
              AND (pg_catalog.has_table_privilege(c.oid, 'INSERT') OR pg_catalog.has_table_privilege(c.oid, 'UPDATE')
                   OR pg_catalog.has_table_privilege(c.oid, 'DELETE') OR pg_catalog.has_table_privilege(c.oid, 'TRUNCATE')))
            OR EXISTS (SELECT 1 FROM pg_catalog.pg_namespace s WHERE s.nspname NOT IN ('pg_catalog', 'information_schema') AND left(s.nspname, 3) <> 'pg_'
                       AND pg_catalog.has_schema_privilege(s.oid, 'CREATE'))
        """;

    /// <summary>Whether the credential can insert, update, delete or truncate in a table of any schema of the application, or create in one of them.</summary>
    /// <param name="dataSource">The source of the connections whose credential is asked about.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public static Task<bool> CanWriteAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        return AskAsync(dataSource, cancellationToken);
    }

    private static async Task<bool> AskAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(Writes);
        await using (command.ConfigureAwait(false))
        {
            return (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
        }
    }
}
