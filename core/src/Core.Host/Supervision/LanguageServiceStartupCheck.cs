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
            StartOutcome.ProcessExited => CheckResult.Failed(
                Name, $"o serviço encerrou com código {supervisor.ExitCode} antes de responder", ExecutablePath),
            StartOutcome.TimedOut => CheckResult.Failed(Name, $"o serviço não respondeu em {seconds} s", ExecutablePath),
            _ => throw new UnreachableException(),
        };
    }
}
