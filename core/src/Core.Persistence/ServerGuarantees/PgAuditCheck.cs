using Core.Platform.Startup;
using Npgsql;

namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// Checks that the audit extension runs in the server: pgaudit loaded at startup, which only shared_preload_libraries can do,
/// and installed in the database. Its own setting proves the load, since reading shared_preload_libraries takes a superuser.
/// </summary>
public sealed class PgAuditCheck : IStartupCheck
{
    /// <summary>A placeholder anyone may set has the user context; only the loaded library defines the setting for superusers.</summary>
    private const string Query = """
        SELECT (SELECT s.setting FROM pg_settings s WHERE s.name = 'pgaudit.log' AND s.context = 'superuser') AS log,
            (SELECT e.extversion FROM pg_extension e WHERE e.extname = 'pgaudit') AS version
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

    internal static Finding Judge(string? logSetting, string? extensionVersion) => (logSetting, extensionVersion) switch
    {
        (null, _) => Finding.Missing(
            "o pgaudit não está carregado no servidor: acrescente pgaudit a shared_preload_libraries no postgresql.conf e reinicie o servidor"),
        (_, null) => Finding.Missing("a extensão pgaudit não está instalada no banco: execute CREATE EXTENSION pgaudit no banco da aplicação"),
        _ => Finding.Holds($"pgaudit {extensionVersion} carregado e instalado; pgaudit.log = '{logSetting}'"),
    };

    private static Task<Finding> ReadAsync(NpgsqlConnection connection, CancellationToken cancellationToken) =>
        ServerProbe.ReadRowAsync(
            connection, server => new NpgsqlCommand(Query, server), row => Judge(row["log"] as string, row["version"] as string), cancellationToken);
}
