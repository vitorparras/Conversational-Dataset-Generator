using Core.Platform.Startup;
using Npgsql;

namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// Checks that every table with a row policy applies it with FORCE, so that the policy holds even against the table's owner;
/// a policy on a table whose row security is off counts as a policy that does not apply.
/// </summary>
public sealed class RlsForceCheck : IStartupCheck
{
    private const string Query = """
        SELECT count(*) FILTER (WHERE c.relrowsecurity AND c.relforcerowsecurity) AS forced,
            min(c.oid::regclass::text) FILTER (WHERE NOT (c.relrowsecurity AND c.relforcerowsecurity)) AS unforced
        FROM pg_class c
        WHERE c.relkind IN ('r', 'p')
            AND (c.relrowsecurity OR EXISTS (SELECT FROM pg_policy p WHERE p.polrelid = c.oid))
        """;

    private readonly NpgsqlDataSource dataSource;

    /// <summary>The check reads through the data source built with the application's credential.</summary>
    public RlsForceCheck(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    /// <inheritdoc />
    public string Name => "RlsForce";

    /// <inheritdoc />
    public Task<CheckResult> RunAsync(CancellationToken cancellationToken) =>
        ServerProbe.RunAsync(
            dataSource,
            new Guarantee(Name, "use um servidor PostgreSQL que ofereça FORCE ROW LEVEL SECURITY", ReadAsync),
            cancellationToken);

    internal static Finding Judge(long forcedTables, string? unforcedTable) =>
        unforcedTable is null
            ? Finding.Holds($"{forcedTables} tabela(s) com política de linha, todas com FORCE ROW LEVEL SECURITY")
            : Finding.Missing(
                $"a tabela {unforcedTable} tem política de linha que não vale contra o dono: execute "
                + $"ALTER TABLE {unforcedTable} ENABLE ROW LEVEL SECURITY e ALTER TABLE {unforcedTable} FORCE ROW LEVEL SECURITY");

    private static Task<Finding> ReadAsync(NpgsqlConnection connection, CancellationToken cancellationToken) =>
        ServerProbe.ReadRowAsync(
            connection, server => new NpgsqlCommand(Query, server), row => Judge((long)row["forced"], row["unforced"] as string), cancellationToken);
}
