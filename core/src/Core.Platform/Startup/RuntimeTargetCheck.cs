using Core.Platform.Versions;

namespace Core.Platform.Startup;

/// <summary>Refuses an executable published for one target that runs on another, naming the target and the machine.</summary>
public sealed class RuntimeTargetCheck : IStartupCheck
{
    private readonly VersionValue publishedFor;
    private readonly string runningOn;

    /// <summary>The target the build stamped, absent when it was built for none, and the target of this machine.</summary>
    public RuntimeTargetCheck(VersionValue publishedFor, string runningOn)
    {
        ArgumentNullException.ThrowIfNull(publishedFor);
        ArgumentException.ThrowIfNullOrWhiteSpace(runningOn);
        this.publishedFor = publishedFor;
        this.runningOn = runningOn;
    }

    /// <inheritdoc />
    public string Name => "RuntimeIdentifier";

    /// <inheritdoc />
    public Task<CheckResult> RunAsync(CancellationToken cancellationToken) => Task.FromResult(Conclude(publishedFor.Text));

    private CheckResult Conclude(string? target) => target switch
    {
        null => CheckResult.Passed(Name, "construção sem alvo declarado: roda onde a plataforma de execução estiver instalada", runningOn),
        _ when string.Equals(target, runningOn, StringComparison.Ordinal) =>
            CheckResult.Passed(Name, $"publicado para o alvo {target}, que é o destino em que roda", target),
        _ => CheckResult.Failed(
            Name, $"publicado para o alvo {target}, mas o destino em que roda é {runningOn}", $"um executável publicado para {runningOn}"),
    };
}
