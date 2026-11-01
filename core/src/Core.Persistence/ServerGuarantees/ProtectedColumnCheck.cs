using Core.Platform.Startup;
using Npgsql;

namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// Refuses to start when a column of the content schema is neither sealed with the mark of its purpose nor listed as stored in clear, or is listed one way and
/// built the other: a column that holds text of material and escapes the path of encryption writes in clear, nothing fails, and it shows only when someone reads a dump.
/// </summary>
public sealed class ProtectedColumnCheck : IStartupCheck
{
    private const string Query = """
        SELECT coalesce(pg_catalog.array_agg(
            c.relname || '|' || a.attname || '|' || pg_catalog.format_type(a.atttypid, a.atttypmod) || '|' || coalesce(pg_catalog.col_description(c.oid, a.attnum), '')
            ORDER BY c.relname, a.attnum), ARRAY[]::text[]) AS columns
        FROM pg_catalog.pg_attribute a
        JOIN pg_catalog.pg_class c ON c.oid = a.attrelid
        JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'content' AND c.relkind IN ('r', 'p') AND a.attnum > 0 AND NOT a.attisdropped
        """;

    private readonly NpgsqlDataSource dataSource;

    /// <summary>The check reads the catalog through the data source built with the application's credential.</summary>
    public ProtectedColumnCheck(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    /// <inheritdoc />
    public string Name => "ProtectedColumns";

    /// <inheritdoc />
    public Task<CheckResult> RunAsync(CancellationToken cancellationToken) =>
        ServerProbe.RunAsync(
            dataSource,
            new Guarantee(Name, "rode o modo migrate com a credencial de dono, que cria as tabelas de conteúdo", ReadAsync),
            cancellationToken);

    internal static Finding Judge(IEnumerable<string> described)
    {
        var found = described.Select(StoredColumn.Parse).ToList();
        var message = found.Select(ProtectedColumnFindings.Stray).FirstOrDefault(stray => stray is not null)
            ?? ProtectedColumnFindings.Lacking(found);
        return message is null
            ? Finding.Holds($"{found.Count} coluna(s) de conteúdo, todas protegidas com a marca da finalidade ou listadas em claro")
            : Finding.Missing(message);
    }

    private static Task<Finding> ReadAsync(NpgsqlConnection connection, CancellationToken cancellationToken) =>
        ServerProbe.ReadRowAsync(connection, Query, row => Judge((string[])row["columns"]), cancellationToken);
}
