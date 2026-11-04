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
    private readonly Func<CancellationToken, Task<string?>>? sealWarning;

    /// <summary>The service as the host builds it: over the data source of the startup container, which a host with no database does not have.</summary>
    internal ChainVerificationService(IServiceProvider host)
        : this(host, VerifierOf(host))
    {
    }

    private ChainVerificationService(IServiceProvider host, CustodyVerifier? verifier)
        : this(
              verifier is null ? null : verifier.VerifyAsync,
              host.GetRequiredService<VerificationSchedule>(),
              host.GetRequiredService<TimeProvider>(),
              host.GetRequiredService<ILoggerFactory>(),
              WarningOf(host),
              host.GetRequiredService<SealWarningState>())
    {
        Verifier = verifier;
        var source = DataSourceOf(host);
        Reader = source is null ? null : new RecordedVerificationReader(source);
        SealReader = source is null ? null : new RecordedSealReader(source);
        Sealer = source is null ? null : new ChainSealer(source, host.GetRequiredService<TimeProvider>());
    }

    /// <summary>The service over the verification it is given, with the clock and the log of its caller.</summary>
    internal ChainVerificationService(Func<CancellationToken, Task<ChainVerification>>? verify, VerificationSchedule schedule, TimeProvider time, ILoggerFactory loggers)
        : this(verify, schedule, time, loggers, null, new SealWarningState())
    {
    }

    /// <summary>The service over the verification, the reading of the warning of the seal and the state that keeps the warning, all given, with the clock and the log of its caller.</summary>
    internal ChainVerificationService(
        Func<CancellationToken, Task<ChainVerification>>? verify,
        VerificationSchedule schedule,
        TimeProvider time,
        ILoggerFactory loggers,
        Func<CancellationToken, Task<string?>>? sealWarning,
        SealWarningState warning)
    {
        this.sealWarning = sealWarning;
        SealWarning = warning;
        this.verify = verify;
        Schedule = schedule;
        this.time = time;
        this.loggers = loggers;
    }

    /// <summary>How often the chain is verified and when a verification is late.</summary>
    internal VerificationSchedule Schedule { get; }

    /// <summary>The reading of the last verification the chain recorded; null when no database is declared.</summary>
    internal RecordedVerificationReader? Reader { get; }

    /// <summary>The verification on demand, which compares the chain with a seal; null when no database is declared.</summary>
    internal CustodyVerifier? Verifier { get; }

    /// <summary>The reading of when the chain last recorded a seal; null when no database is declared.</summary>
    internal RecordedSealReader? SealReader { get; }

    /// <summary>The making and the record of a seal by whoever operates; null when no database is declared.</summary>
    internal ChainSealer? Sealer { get; }

    /// <summary>Where the warning of the manual situation of the seal is kept, as the last round or the last record of a seal left it.</summary>
    internal SealWarningState SealWarning { get; }

    /// <summary>Whether a verification recorded at the moment given is late, by the clock the service was given.</summary>
    internal bool IsOverdue(DateTimeOffset? lastRecordedAt) => Schedule.Overdue(lastRecordedAt, time.GetUtcNow());

    /// <summary>
    /// Verifies the chain on demand against the seal given and tells the log what it found: a chain that does not hold, and a seal that does not, are errors, and neither writes a failure
    /// of its own nor blocks a write. Null when no database is declared or the verification did not reach its end, which the log tells with the type of the failure.
    /// </summary>
    internal async Task<ChainVerification?> VerifyAgainstAsync(ChainSeal seal, CancellationToken cancellationToken)
    {
        if (Verifier is null)
        {
            return null;
        }

        var logger = loggers.CreateLogger<ChainVerificationService>();
        try
        {
            var result = await Verifier.VerifyAsync(seal, cancellationToken).ConfigureAwait(false);
            Report(logger, result);
            if (result.Seal is { Holds: false } failing)
            {
                logger.SealDoesNotHold(failing.Outcome, seal.Events, result.EventsRead);
            }

            return result;
        }
        catch (Exception failure) when (ServingFailure.CanReport(failure) && !cancellationToken.IsCancellationRequested)
        {
            logger.SealVerificationFailed(failure.GetType().Name);
            return null;
        }
    }

    /// <summary>Reads again when the chain last recorded a seal and takes the warning that follows; the situations that send the seal themselves, and a host with no database, have none.</summary>
    /// <param name="cancellationToken">Cancels the reading.</param>
    internal async Task RefreshSealWarningAsync(CancellationToken cancellationToken)
    {
        if (sealWarning is not null)
        {
            SealWarning.Record(await sealWarning(cancellationToken).ConfigureAwait(false));
        }
    }

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

    private static Func<CancellationToken, Task<string?>>? WarningOf(IServiceProvider host)
    {
        var situation = host.GetRequiredService<SealDeclaration>().Situation;
        if (situation != SealSituation.Manual || DataSourceOf(host) is not { } source)
        {
            return null;
        }

        var reader = new RecordedSealReader(source);
        var time = host.GetRequiredService<TimeProvider>();
        return async token => SealAge.Warning(situation, await reader.LastAsync(token).ConfigureAwait(false), time.GetUtcNow());
    }

    private static CustodyVerifier? VerifierOf(IServiceProvider host) =>
        DataSourceOf(host) is { } source ? new CustodyVerifier(source, host.GetRequiredService<TimeProvider>()) : null;

    private static void Report(ILogger logger, ChainVerification result)
    {
        if (result.FirstInconsistent is { } broken)
        {
            logger.ChainBroken($"{broken.Sequence} ({broken.Kind})", result.EventsRead, result.Recorded.Outcome);
        }
        else
        {
            logger.ChainHeld(result.EventsRead, result.Recorded.Outcome);
        }
    }

    private async Task VerifyOnceAsync(Func<CancellationToken, Task<ChainVerification>> verification, CancellationToken cancellationToken)
    {
        var logger = loggers.CreateLogger<ChainVerificationService>();
        try
        {
            Report(logger, await verification(cancellationToken).ConfigureAwait(false));
        }
        catch (Exception failure) when (ServingFailure.CanReport(failure) && !cancellationToken.IsCancellationRequested)
        {
            logger.VerificationFailed(failure.GetType().Name);
        }

        try
        {
            await RefreshSealWarningAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception failure) when (ServingFailure.CanReport(failure) && !cancellationToken.IsCancellationRequested)
        {
            logger.VerificationFailed(failure.GetType().Name);
        }
    }
}
