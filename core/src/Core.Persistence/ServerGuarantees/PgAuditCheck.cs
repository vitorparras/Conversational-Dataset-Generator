using System.Data;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// Checks that the audit extension runs in the server: pgaudit loaded at startup, which only shared_preload_libraries can do,
/// installed in the database, logging what the application's role reads and writes, and keeping out of the log the parameters of a statement,
/// on error too, the detail of an error and the text of a query, with the audit lines kept where the server log holds them.
/// Its own setting proves the load, since reading shared_preload_libraries takes a superuser.
/// </summary>
public sealed class PgAuditCheck : IStartupCheck
{
    /// <summary>
    /// A placeholder set by a connection option or a configuration file never shows in pg_settings; the library's setting does. The
    /// settings are read as the application's role sees them, so a setting made for that role counts.
    /// </summary>
    private const string Query = """
        SELECT (SELECT s.setting FROM pg_catalog.pg_settings s WHERE s.name = 'pgaudit.log' AND s.context = 'superuser') AS log,
            (SELECT s.setting FROM pg_catalog.pg_settings s WHERE s.name = 'pgaudit.log_parameter' AND s.context = 'superuser') AS log_parameter,
            (SELECT s.setting FROM pg_catalog.pg_settings s WHERE s.name = 'pgaudit.log_relation' AND s.context = 'superuser') AS log_relation,
            (SELECT s.setting FROM pg_catalog.pg_settings s WHERE s.name = 'log_parameter_max_length') AS parameter_max_length,
            (SELECT s.setting FROM pg_catalog.pg_settings s WHERE s.name = 'log_parameter_max_length_on_error') AS parameter_max_length_on_error,
            (SELECT s.setting FROM pg_catalog.pg_settings s WHERE s.name = 'log_min_messages') AS min_messages,
            (SELECT s.setting FROM pg_catalog.pg_settings s WHERE s.name = 'pgaudit.log_client') AS log_client,
            (SELECT s.setting FROM pg_catalog.pg_settings s WHERE s.name = 'pgaudit.log_level') AS log_level,
            (SELECT pg_catalog.string_agg(s.name, ', ' ORDER BY s.name) FROM pg_catalog.pg_settings s
                WHERE s.name IN ('debug_print_parse', 'debug_print_rewritten', 'debug_print_plan') AND s.setting = 'on') AS debug_prints,
            (SELECT e.extversion FROM pg_catalog.pg_extension e WHERE e.extname = 'pgaudit') AS version
        """;

    private readonly NpgsqlDataSource dataSource;

    /// <summary>The check reads through the data source built with the application's credential.</summary>
    public PgAuditCheck(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    /// <inheritdoc />
    public string Name => "PgAudit";

    /// <inheritdoc />
    public Task<CheckResult> RunAsync(CancellationToken cancellationToken) =>
        ServerProbe.RunAsync(
            dataSource, new Guarantee(Name, "o papel da aplicação precisa ler pg_settings e pg_extension", ReadAsync), cancellationToken);

    internal static Finding Judge(AuditSettings settings, string? extensionVersion)
    {
        if (settings.Log is null)
        {
            return Finding.Missing(
                "o pgaudit não está carregado no servidor: acrescente pgaudit a shared_preload_libraries no postgresql.conf e reinicie o servidor");
        }

        if (extensionVersion is null)
        {
            return Finding.Missing("a extensão pgaudit não está instalada no banco: execute CREATE EXTENSION pgaudit no banco da aplicação");
        }

        var gaps = settings.Gaps();
        return gaps.Count > 0
            ? Finding.Missing(string.Join("; ", gaps))
            : Finding.Holds($"pgaudit {extensionVersion} carregado e instalado; pgaudit.log = '{settings.Log}', parâmetros fora do log");
    }

    private static Task<Finding> ReadAsync(NpgsqlConnection connection, CancellationToken cancellationToken) =>
        ServerProbe.ReadRowAsync(connection, Query, Judged, cancellationToken);

    private static Finding Judged(IDataRecord row) =>
        Judge(
            new AuditSettings(
                Text(row, "log"),
                Text(row, "log_parameter"),
                Text(row, "parameter_max_length"),
                Text(row, "parameter_max_length_on_error"),
                Text(row, "min_messages"),
                Text(row, "log_client"),
                Text(row, "log_level"),
                Text(row, "debug_prints"),
                Text(row, "log_relation")),
            Text(row, "version"));

    private static string? Text(IDataRecord row, string column) => row[column] as string;
}
