using Core.Platform.Startup;

namespace Core.Platform.Logging;

/// <summary>Reports whether the log file the configuration names could be opened; the result names the key, never the path.</summary>
public sealed class LogFileCheck : IStartupCheck
{
    private readonly bool opened;

    /// <summary>The check is named by the configuration key that holds the path.</summary>
    public LogFileCheck(string name, bool opened)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        this.opened = opened;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public Task<CheckResult> RunAsync(CancellationToken cancellationToken) =>
        Task.FromResult(opened ? Opened() : Unwritable());

    private CheckResult Opened() => CheckResult.Passed(Name, "arquivo de registro aberto para escrita", Name);

    private CheckResult Unwritable() => CheckResult.Failed(
        Name,
        $"o arquivo de registro declarado em '{Name}' não pôde ser aberto para escrita (pasta inexistente ou sem permissão)",
        Name);
}
