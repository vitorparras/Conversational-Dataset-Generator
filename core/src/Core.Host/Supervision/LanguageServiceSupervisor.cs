using System.Security.Cryptography;

namespace Core.Host.Supervision;

/// <summary>Owns the language service for as long as the core lives: its process, and the secret the core and the service share.</summary>
internal sealed class LanguageServiceSupervisor : IDisposable
{
    private const int SecretBytes = 32;

    private readonly ServiceProcessLauncher launcher;
    private readonly HealthWait health;
    private readonly ServiceProcessWatch watch;
    private readonly ILogger<LanguageServiceSupervisor> logger;
    private RunningService? service;

    internal LanguageServiceSupervisor(
        ServiceProcessLauncher launcher, HealthWait health, ServiceProcessWatch watch, ILogger<LanguageServiceSupervisor> logger)
    {
        this.launcher = launcher;
        this.health = health;
        this.watch = watch;
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

    /// <summary>Starts the service with a new secret and waits for it to answer; a start that does not end with an answer, cancelled ones included, leaves the service unavailable.</summary>
    internal async Task<StartOutcome> StartAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        StartOutcome? outcome = null;
        try
        {
            outcome = await LaunchAsync(timeout, cancellationToken);
            return outcome.Value;
        }
        finally
        {
            if (outcome != StartOutcome.Started)
            {
                watch.Move(ServiceState.Unavailable);
            }
        }
    }

    /// <summary>
    /// Ends the current process and starts another with a new secret, the only one the new process accepts;
    /// the time limit covers the wait for health, not the end of the previous process, which takes up to its grace period.
    /// The end is logged with the state read once the start returns, so a process that dies right after answering is logged as unavailable.
    /// </summary>
    internal async Task<StartOutcome> RestartAsync(RestartRequest request, TimeSpan timeout, CancellationToken cancellationToken)
    {
        watch.Move(ServiceState.Restarting);
        logger.RestartRequested(request.Requester, request.Reference);
        try
        {
            Release();
            return await StartAsync(timeout, cancellationToken);
        }
        finally
        {
            logger.RestartEnded(watch.State, request.Reference);
        }
    }

    /// <summary>Marks the service stopped and closes its standard input, which is how it learns the core is gone.</summary>
    public void Dispose()
    {
        watch.Move(ServiceState.Stopped);
        Release();
    }

    /// <summary>One attempt: a new secret, a new process and the wait for its health; the process that answers is watched from then on.</summary>
    private async Task<StartOutcome> LaunchAsync(TimeSpan timeout, CancellationToken cancellationToken)
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
                watch.Watch(started, Lost);
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
    private void Release()
    {
        var previous = service;
        service = null;
        Secret = string.Empty;
        if (previous is null)
        {
            return;
        }

        var processId = previous.ProcessId;
        previous.Dispose();
        logger.ServiceStopped(processId);
    }

    private void Lost(RunningService ended) => logger.ServiceLost(ended.ProcessId, ended.ExitCode);
}
