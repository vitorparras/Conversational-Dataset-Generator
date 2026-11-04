using Core.Platform.Startup;
using Npgsql;

namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// Refuses the start while the window of the load of a restore is open (#64): whoever was loading died before closing it, so the restore did not conclude and the database holds a copy that was
/// never conferred. The window closes only by the load, approved or not; a database in this state is discarded and made again by the migration.
/// </summary>
public sealed class RestoreWindowCheck : IStartupCheck
{
    private const string Query = "SELECT pg_catalog.count(*) FILTER (WHERE state = 'opened') AS opened, pg_catalog.count(*) FILTER (WHERE state = 'closed') AS closed FROM public.restore_window";

    private readonly NpgsqlDataSource dataSource;

    /// <summary>The check reads through the data source built with the application's credential.</summary>
    public RestoreWindowCheck(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    /// <inheritdoc />
    public string Name => "RestoreWindow";

    /// <inheritdoc />
    public Task<CheckResult> RunAsync(CancellationToken cancellationToken) =>
        ServerProbe.RunAsync(
            dataSource,
            new Guarantee(Name, "aplique as migrações do núcleo com o migrate, que criam a tabela da janela de carga", ReadAsync, ReadsCatalog: false),
            cancellationToken);

    internal static Finding Judge(long opened, long closed) =>
        opened > 0 && closed == 0
            ? Finding.Missing(
                "a janela de carga de uma restauração está aberta: a restauração não concluiu, e a cópia carregada não foi conferida; "
                + "descarte este banco, crie outro com o migrate e carregue a cópia de novo")
            : Finding.Holds("nenhuma janela de carga de restauração está aberta");

    private static Task<Finding> ReadAsync(NpgsqlConnection connection, CancellationToken cancellationToken) =>
        ServerProbe.ReadRowAsync(connection, Query, row => Judge(row.GetInt64(0), row.GetInt64(1)), cancellationToken);
}
