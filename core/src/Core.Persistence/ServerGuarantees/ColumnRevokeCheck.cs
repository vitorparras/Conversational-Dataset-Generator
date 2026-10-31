using System.Data;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// Checks that write permission granted column by column is not voided: where a table grants INSERT or UPDATE on its
/// columns, the application's role must not hold the same privilege on the whole table, which would override every column.
/// </summary>
public sealed class ColumnRevokeCheck : IStartupCheck
{
    private const string Query = """
        WITH granted AS (
            SELECT DISTINCT c.oid, c.oid::regclass::text AS relation, acl.privilege_type
            FROM pg_catalog.pg_class c
            JOIN pg_catalog.pg_attribute a ON a.attrelid = c.oid AND a.attacl IS NOT NULL
            CROSS JOIN LATERAL pg_catalog.aclexplode(a.attacl) acl
            WHERE c.relkind IN ('r', 'p') AND acl.privilege_type IN ('INSERT', 'UPDATE'))
        SELECT current_user::text AS role, (SELECT pg_catalog.count(DISTINCT oid) FROM granted) AS tables, voided.privilege_type, voided.relation
        FROM (SELECT) AS answer
        LEFT JOIN LATERAL (
            SELECT g.privilege_type, g.relation FROM granted g
            WHERE pg_catalog.has_table_privilege(g.oid, g.privilege_type)
            ORDER BY g.relation, g.privilege_type
            LIMIT 1) voided ON true
        """;

    private readonly NpgsqlDataSource dataSource;

    /// <summary>The check reads through the data source built with the application's credential.</summary>
    public ColumnRevokeCheck(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    /// <inheritdoc />
    public string Name => "ColumnRevoke";

    /// <inheritdoc />
    public Task<CheckResult> RunAsync(CancellationToken cancellationToken) =>
        ServerProbe.RunAsync(
            dataSource,
            new Guarantee(Name, "use um servidor PostgreSQL que ofereça permissão por coluna (GRANT e REVOKE com lista de colunas)", ReadAsync),
            cancellationToken);

    internal static Finding Judge(string role, long tablesWithColumnGrants, string? privilege, string? table) =>
        privilege is null
            ? Finding.Holds($"{tablesWithColumnGrants} tabela(s) com permissão por coluna, nenhuma anulada por concessão na tabela inteira")
            : Finding.Missing(
                $"o papel '{role}' tem {privilege} na tabela {table} inteira, o que anula a permissão por coluna: execute "
                + $"REVOKE {privilege} ON {table} FROM {role} (e FROM PUBLIC, se a concessão vier de lá) e conceda {privilege} "
                + "só nas colunas permitidas");

    private static Task<Finding> ReadAsync(NpgsqlConnection connection, CancellationToken cancellationToken) =>
        ServerProbe.ReadRowAsync(connection, Query, Read, cancellationToken);

    private static Finding Read(IDataRecord row) =>
        Judge((string)row["role"], (long)row["tables"], row["privilege_type"] as string, row["relation"] as string);
}
