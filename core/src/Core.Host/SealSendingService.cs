using Core.Boundary.Http;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Verification;
using Npgsql;

namespace Core.Host;

/// <summary>
/// Sends the seal of the chain to the destination the installation declared, once a day: the seal of the tip is made, its text is sent, and only when it arrived is it recorded in the chain,
/// as the seal that was sent and not as the tip of the moment of the record. A failure of any kind writes nothing, is told in the log with its reason and the count of the seal, and the next
/// try comes at the next interval. The manual situation has nothing to send, and a host with no database has no chain: the service then does nothing.
/// </summary>
internal sealed class SealSendingService : BackgroundService
{
    /// <summary>The time from one sending to the next; after a start, what is left of it since the last seal that the chain recorded.</summary>
    internal static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    private readonly SealSteps? steps;
    private readonly TimeProvider time;
    private readonly ILoggerFactory loggers;
    private readonly SealDelivery? delivery;

    /// <summary>The service as the host builds it: it sends when the situation has a destination and the host has a database.</summary>
    internal SealSendingService(IServiceProvider host)
        : this(host, DeliveryOf(host))
    {
    }

    private SealSendingService(IServiceProvider host, SealDelivery? delivery)
        : this(StepsOf(host, delivery), host.GetRequiredService<TimeProvider>(), host.GetRequiredService<ILoggerFactory>())
    {
        this.delivery = delivery;
    }

    /// <summary>The service over the steps it is given, with the clock and the log of its caller; no steps, nothing to send.</summary>
    internal SealSendingService(SealSteps? steps, TimeProvider time, ILoggerFactory loggers)
    {
        this.steps = steps;
        this.time = time;
        this.loggers = loggers;
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        delivery?.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (steps is null)
        {
            return;
        }

        try
        {
            await Task.Delay(await WaitAsync(steps, stoppingToken).ConfigureAwait(false), time, stoppingToken).ConfigureAwait(false);
            while (true)
            {
                await SendOnceAsync(steps, stoppingToken).ConfigureAwait(false);
                await Task.Delay(Interval, time, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping: ending is not a failure.
        }
    }

    private static SealDelivery? DeliveryOf(IServiceProvider host) =>
        host.GetRequiredService<SealDeclaration>().Destination is { } destination ? new SealDelivery(destination) : null;

    private static SealSteps? StepsOf(IServiceProvider host, SealDelivery? delivery)
    {
        var source = host.GetRequiredService<ServiceProvider>().GetService<NpgsqlDataSource>();
        if (delivery is null || source is null)
        {
            return null;
        }

        var sealer = new ChainSealer(source, host.GetRequiredService<TimeProvider>());
        return new SealSteps(sealer.MakeAsync, delivery.SendAsync, (sent, token) => sealer.RecordAsync(sent, CustodyAuthor.System, token), new RecordedSealReader(source).LastAsync);
    }

    /// <summary>What is left of the interval since the last seal the chain recorded, so that a restart does not send another at once; none when it cannot be read.</summary>
    private async Task<TimeSpan> WaitAsync(SealSteps toSend, CancellationToken cancellationToken)
    {
        try
        {
            var last = await toSend.Last(cancellationToken).ConfigureAwait(false);
            return last is { } recorded && recorded + Interval > time.GetUtcNow() ? recorded + Interval - time.GetUtcNow() : TimeSpan.Zero;
        }
        catch (Exception failure) when (ServingFailure.CanReport(failure) && !cancellationToken.IsCancellationRequested)
        {
            loggers.CreateLogger<SealSendingService>().SealSendingFailed(failure.GetType().Name);
            return TimeSpan.Zero;
        }
    }

    private async Task SendOnceAsync(SealSteps toSend, CancellationToken cancellationToken)
    {
        var logger = loggers.CreateLogger<SealSendingService>();
        try
        {
            var made = await toSend.Make(cancellationToken).ConfigureAwait(false);
            var sent = await toSend.Send(made.ToText(), cancellationToken).ConfigureAwait(false);
            if (sent.Delivered)
            {
                Report(logger, made, await toSend.Record(made, cancellationToken).ConfigureAwait(false));
            }
            else
            {
                logger.SealNotDelivered(sent.Failure, sent.Status, made.Events);
            }
        }
        catch (Exception failure) when (ServingFailure.CanReport(failure) && !cancellationToken.IsCancellationRequested)
        {
            logger.SealSendingFailed(failure.GetType().Name);
        }
    }

    private static void Report(ILogger logger, ChainSeal made, SealRecording recording)
    {
        if (recording.Seal is null)
        {
            logger.SealSentButNotRecorded(made.Events, recording.Recorded.Outcome);
        }
        else
        {
            logger.SealSentAndRecorded(made.Events, recording.Recorded.Sequence);
        }
    }
}
