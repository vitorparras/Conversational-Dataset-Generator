using Npgsql;

namespace Core.Persistence.Settings;

/// <summary>
/// What the credential of a connection may do to the database: the applier of the configuration reads and
/// nothing else (decision 0053), and it asks before it applies anything. Only what is granted to the role or
/// to a role it belongs to counts, or owned by one: the grants to PUBLIC of the migrations are what the
/// application's own role works through and every role holds them.
/// </summary>
public static class CredentialPrivileges
{
    private const string Writes = """
        SELECT EXISTS (
            SELECT 1 FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname NOT IN ('pg_catalog', 'information_schema') AND left(n.nspname, 3) <> 'pg_' AND c.relkind IN ('r', 'p')
              AND (pg_catalog.pg_has_role(current_user, c.relowner, 'USAGE')
                   OR EXISTS (SELECT 1 FROM pg_catalog.aclexplode(c.relacl) a
                              WHERE a.grantee <> 0 AND a.privilege_type IN ('INSERT', 'UPDATE', 'DELETE', 'TRUNCATE') AND pg_catalog.pg_has_role(current_user, a.grantee, 'USAGE'))))
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
