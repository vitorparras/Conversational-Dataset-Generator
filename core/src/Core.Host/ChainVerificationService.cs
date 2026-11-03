using Core.Persistence.Custody.Verification;
using Npgsql;

namespace Core.Host;

/// <summary>
/// Verifies the chain of custody as soon as the host is up and then once every interval, one after the other: the next waits the interval after the one before ended, so two never overlap.
/// It is not a startup check: a broken chain does not stop the host nor change its readiness, because reading must go on to investigate it. The verdict goes to the log,
/// as an error when the chain does not hold, and a verification that does not reach its end is logged with the type of the failure and tried again at the next interval.
/// With no database declared there is no chain: the service does nothing and has nothing to read.
/// </summary>
internal sealed class ChainVerificationService : BackgroundService
{
    private readonly Func<CancellationToken, Task<ChainVerification>>? verify;
    private readonly TimeProvider time;
    private readonly ILoggerFactory loggers;

    /// <summary>The service as the host builds it: over the data source of the startup container, which a host with no database does not have.</summary>
    internal ChainVerificationService(IServiceProvider host)
        : this(VerifierOf(host), host.GetRequiredService<VerificationSchedule>(), host.GetRequiredService<TimeProvider>(), host.GetRequiredService<ILoggerFactory>())
    {
        Reader = DataSourceOf(host) is { } source ? new RecordedVerificationReader(source) : null;
    }

    /// <summary>The service over the verification it is given, with the clock and the log of its caller.</summary>
    internal ChainVerificationService(Func<CancellationToken, Task<ChainVerification>>? verify, VerificationSchedule schedule, TimeProvider time, ILoggerFactory loggers)
    {
        this.verify = verify;
        Schedule = schedule;
        this.time = time;
        this.loggers = loggers;
    }

    /// <summary>How often the chain is verified and when a verification is late.</summary>
    internal VerificationSchedule Schedule { get; }

    /// <summary>The reading of the last verification the chain recorded; null when no database is declared.</summary>
    internal RecordedVerificationReader? Reader { get; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (verify is null)
        {
            return;
        }

        try
        {
            while (true)
            {
                await VerifyOnceAsync(verify, stoppingToken).ConfigureAwait(false);
                await Task.Delay(Schedule.Interval, time, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping: ending is not a failure.
        }
    }

    private static NpgsqlDataSource? DataSourceOf(IServiceProvider host) => host.GetRequiredService<ServiceProvider>().GetService<NpgsqlDataSource>();

    private static Func<CancellationToken, Task<ChainVerification>>? VerifierOf(IServiceProvider host) =>
        DataSourceOf(host) is { } source ? new CustodyVerifier(source, host.GetRequiredService<TimeProvider>()).VerifyAsync : null;

    private async Task VerifyOnceAsync(Func<CancellationToken, Task<ChainVerification>> verification, CancellationToken cancellationToken)
    {
        var logger = loggers.CreateLogger<ChainVerificationService>();
        try
        {
            var result = await verification(cancellationToken).ConfigureAwait(false);
            if (result.FirstInconsistent is { } broken)
            {
                logger.ChainBroken($"{broken.Sequence} ({broken.Kind})", result.EventsRead, result.Recorded.Outcome);
            }
            else
            {
                logger.ChainHeld(result.EventsRead, result.Recorded.Outcome);
            }
        }
        catch (Exception failure) when (ServingFailure.CanReport(failure) && !cancellationToken.IsCancellationRequested)
        {
            logger.VerificationFailed(failure.GetType().Name);
        }
    }
}
