using Core.Platform.Startup;

namespace Core.Persistence.Connection;

/// <summary>Stands in for the server checks when no database is declared, so the status says so instead of staying silent.</summary>
public sealed class UnconfiguredDatabaseCheck : IStartupCheck
{
    /// <inheritdoc />
    public string Name => "Database";

    /// <inheritdoc />
    public Task<CheckResult> RunAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new CheckResult(
            Name,
            CheckStatus.NotConfigured,
            "banco não configurado: nenhuma garantia do servidor foi conferida; declare Database:Host, Database:Name, "
            + "Database:Username e Database:Password para conferir",
            DatabaseConfiguration.HostPath));
}
