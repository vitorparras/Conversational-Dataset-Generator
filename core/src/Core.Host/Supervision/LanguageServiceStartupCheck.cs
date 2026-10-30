using System.Diagnostics;
using System.Globalization;
using Core.Platform.Startup;

namespace Core.Host.Supervision;

/// <summary>Turns the start of the language service into the result of a check; the service itself belongs to the supervisor.</summary>
internal sealed class LanguageServiceStartupCheck : IStartupCheck
{
    private const string ExecutablePath = LanguageServiceConfiguration.ExecutablePath;

    private readonly LanguageServiceSupervisor supervisor;
    private readonly string timeoutSeconds;

    internal LanguageServiceStartupCheck(LanguageServiceSupervisor supervisor, string timeoutSeconds)
    {
        this.supervisor = supervisor;
        this.timeoutSeconds = timeoutSeconds;
    }

    /// <inheritdoc />
    public string Name => "LanguageService";

    /// <inheritdoc />
    public async Task<CheckResult> RunAsync(CancellationToken cancellationToken)
    {
        if (!int.TryParse(timeoutSeconds, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) || seconds <= 0)
        {
            return CheckResult.Failed(
                Name,
                $"o tempo limite '{timeoutSeconds}' não é um número inteiro positivo de segundos",
                LanguageServiceConfiguration.StartupTimeoutPath);
        }

        var outcome = await supervisor.StartAsync(TimeSpan.FromSeconds(seconds), cancellationToken);
        return outcome switch
        {
            StartOutcome.Started => CheckResult.Passed(Name, "o serviço respondeu à conferência de saúde", ExecutablePath),
            StartOutcome.NotStarted => CheckResult.Failed(Name, "o executável configurado não pôde ser iniciado", ExecutablePath),
            StartOutcome.ProcessExited => Stopped($"o serviço encerrou com código {supervisor.ExitCode} antes de responder"),
            StartOutcome.TimedOut => Stopped($"o serviço não respondeu em {seconds} s"),
            _ => throw new UnreachableException(),
        };
    }

    /// <summary>A start that did not end with an answer, with how far it got where the failure says what was observed.</summary>
    private CheckResult Stopped(string failure) => CheckResult.Failed(Name, failure, ExecutablePath) with { Observed = supervisor.StoppedAt };
}
