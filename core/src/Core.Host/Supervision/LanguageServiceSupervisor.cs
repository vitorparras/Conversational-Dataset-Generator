using System.Security.Cryptography;

namespace Core.Host.Supervision;

/// <summary>Owns the language service for as long as the core lives: its process, and the secret the core and the service share.</summary>
/// <param name="launcher">Starts the process of the service.</param>
/// <param name="health">Waits until the service answers.</param>
/// <param name="watch">Holds the state of the service and watches its process.</param>
/// <param name="logger">Where the start, the restart and the end of the service are written.</param>
internal sealed class LanguageServiceSupervisor(
    ServiceProcessLauncher launcher, HealthWait health, ServiceProcessWatch watch, ILogger<LanguageServiceSupervisor> logger) : IDisposable
{
    private const int SecretBytes = 32;
    private static readonly TimeSpan ErrorDrainWait = TimeSpan.FromSeconds(2);

    private Published? current;

    /// <summary>The secret of the running service; empty until the service has answered and again as soon as it is gone.</summary>
    internal string Secret => Endpoint?.Secret ?? string.Empty;

    /// <summary>The loopback port of the running service; null until the service has answered and again as soon as it is gone, so a port nobody answers for is never handed out.</summary>
    internal int? Port => Endpoint?.Port;

    /// <summary>
    /// The port and the secret of the service alive now. Both come from the one value published when the service answered, replaced whole at every start and
    /// restart, so a reader never gets the port of one process with the secret of another; null when there is none or its process has ended.
    /// </summary>
    internal ServiceEndpoint? Endpoint => Live?.Endpoint;

    /// <summary>What is published while the service is available and its process has not ended; the end is read from the process, not from the watch that learns of it later.</summary>
    private Published? Live => Volatile.Read(ref current) is { } published && watch.State == ServiceState.Available && !published.Process.HasExited ? published : null;

    /// <summary>The process number of the running service; null until the service has answered.</summary>
    internal int? ProcessId => Volatile.Read(ref current)?.Process.ProcessId;

    /// <summary>The exit code of the process when it ended before answering; null otherwise.</summary>
    internal int? ExitCode { get; private set; }

    /// <summary>
    /// The end of the standard error of the process when it ended before answering and the launcher was asked to collect it; null otherwise.
    /// Never written to the log: the service may echo the material it was given.
    /// </summary>
    internal string? ExitErrorTail { get; private set; }

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

            var outcome = await health.UntilHealthyAsync(started, secret, timeout, cancellationToken);
            if (started.Port != 0)
            {
                logger.ServiceStarted(started.ProcessId, started.Port);
            }

            if (outcome == StartOutcome.Started)
            {
                var running = new Published(started, new ServiceEndpoint(started.Port, secret));
                Volatile.Write(ref current, running);
                started = null;
                watch.Watch(running.Process, Lost);
                return outcome;
            }

            logger.ServiceNotReady(outcome);
            ExitCode = outcome == StartOutcome.ProcessExited ? started.ExitCode : null;
            ExitErrorTail = outcome == StartOutcome.ProcessExited && started.Errors is { } errors ? await errors.TextAsync(ErrorDrainWait) : null;
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
        var previous = Interlocked.Exchange(ref current, null);
        if (previous is null)
        {
            return;
        }

        var processId = previous.Process.ProcessId;
        previous.Process.Dispose();
        logger.ServiceStopped(processId);
    }

    private void Lost(RunningService ended) => logger.ServiceLost(ended.ProcessId, ended.ExitCode);

    /// <summary>The process of the service and the endpoint it answers at, born together and replaced together.</summary>
    private sealed record Published(RunningService Process, ServiceEndpoint Endpoint);
}
