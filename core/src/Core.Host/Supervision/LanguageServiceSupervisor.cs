using System.Security.Cryptography;

namespace Core.Host.Supervision;

/// <summary>Owns the language service for as long as the core lives: its process, and the secret the core and the service share.</summary>
internal sealed class LanguageServiceSupervisor : IDisposable
{
    private const int SecretBytes = 32;

    private readonly ServiceProcessLauncher launcher;
    private readonly HealthWait health;
    private readonly ILogger<LanguageServiceSupervisor> logger;
    private RunningService? service;

    internal LanguageServiceSupervisor(ServiceProcessLauncher launcher, HealthWait health, ILogger<LanguageServiceSupervisor> logger)
    {
        this.launcher = launcher;
        this.health = health;
        this.logger = logger;
    }

    /// <summary>The secret of the running service; empty until the service has answered.</summary>
    internal string Secret { get; private set; } = string.Empty;

    /// <summary>The loopback port of the running service; null until the service has answered.</summary>
    internal int? Port => service?.Port;

    /// <summary>The process number of the running service; null until the service has answered.</summary>
    internal int? ProcessId => service?.ProcessId;

    /// <summary>The exit code of the process when it ended before answering; null otherwise.</summary>
    internal int? ExitCode { get; private set; }

    /// <summary>Starts the service with a new secret and waits for it to answer; a service that does not answer is stopped.</summary>
    internal async Task<StartOutcome> StartAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(SecretBytes));
        RunningService? started = null;
        try
        {
            started = launcher.Start(secret);
            if (started is null)
            {
                return StartOutcome.NotStarted;
            }

            logger.ServiceStarted(started.ProcessId, started.Port);
            var outcome = await health.UntilHealthyAsync(started, secret, timeout, cancellationToken);
            if (outcome == StartOutcome.Started)
            {
                service = started;
                started = null;
                Secret = secret;
                return outcome;
            }

            logger.ServiceNotReady(outcome);
            ExitCode = outcome == StartOutcome.ProcessExited ? started.ExitCode : null;
            return outcome;
        }
        finally
        {
            started?.Dispose();
        }
    }

    /// <summary>Closes the standard input of the service, which is how it learns the core is gone, and logs that it ended.</summary>
    public void Dispose()
    {
        if (service is null)
        {
            return;
        }

        var processId = service.ProcessId;
        service.Dispose();
        service = null;
        logger.ServiceStopped(processId);
    }
}
