using Core.Host.Submissions;
using Core.Ingestion.Sanitizing;
using Core.Persistence.Content.Rerun;
using Core.Persistence.Domains;
using Core.Platform.Keys;
using Npgsql;

namespace Core.Host;

/// <summary>
/// Empties the queue of the rerun (issue 97) in the background, one item at a time: in each domain it holds an item of a request to the version of the steps of this process, reruns it over
/// the raw text and concludes it; a rerun that fails spends a try of the item, and with no item in any domain it waits <see cref="Pause"/>. A failure of the database is told in the log by its
/// type and the next round tries again, while the item it held goes back to the queue when its hold expires. With no database there is no queue, and the service then does nothing.
/// </summary>
internal sealed class RerunService(RerunService.Parts? parts, TimeProvider time, ILogger<RerunService> logger) : BackgroundService
{
    /// <summary>How long the worker waits when no domain has an item for it.</summary>
    internal static readonly TimeSpan Pause = TimeSpan.FromSeconds(5);

    internal RerunService(IServiceProvider host, SanitizationRerun rerun)
        : this(PartsOf(host, rerun), host.GetRequiredService<TimeProvider>(), host.GetRequiredService<ILogger<RerunService>>())
    {
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (parts is null)
        {
            return;
        }

        try
        {
            while (true)
            {
                var treated = await RoundAsync(parts, stoppingToken).ConfigureAwait(false);
                await Task.Delay(treated ? TimeSpan.Zero : Pause, time, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping: ending is not a failure.
        }
    }

    private static Parts? PartsOf(IServiceProvider host, SanitizationRerun rerun)
    {
        var startup = host.GetRequiredService<ServiceProvider>();
        return startup.GetService<NpgsqlDataSource>() is { } source
            ? new Parts(new DomainListing(source), new SanitizationReruns(source, host.GetRequiredService<TimeProvider>()), rerun,
                token => OpenAsync(startup, source, host.GetRequiredService<TimeProvider>(), token))
            : null;
    }

    /// <summary>The content opened with the master key and the labels the database registers, for one item; none to rerun with when the key was not read.</summary>
    private static async Task<Access> OpenAsync(IServiceProvider startup, NpgsqlDataSource source, TimeProvider time, CancellationToken cancellationToken)
    {
        var reading = await startup.GetRequiredService<IMasterKeySource>().ReadAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var known = reading.Failure is null ? await RegisteredLabels.ReadAsync(source, cancellationToken).ConfigureAwait(false) : null;
            return new Access(known is null ? null : CompositionRoot.ResanitizerOf(source, CompositionRoot.ContentOf(reading, known), time), reading);
        }
        catch
        {
            reading.Dispose();
            throw;
        }
    }

    /// <summary>One item of each domain that has one; whether some item was treated, so that the next round need not wait.</summary>
    private async Task<bool> RoundAsync(Parts work, CancellationToken cancellationToken)
    {
        try
        {
            var treated = false;
            foreach (var domain in await work.Domains.ListAsync(cancellationToken).ConfigureAwait(false))
            {
                treated |= await TakeAsync(work, domain.Id, cancellationToken).ConfigureAwait(false);
            }

            return treated;
        }
        catch (Exception failure) when (ServingFailure.CanReport(failure) && !cancellationToken.IsCancellationRequested)
        {
            logger.RerunInterrupted(failure.GetType().Name);
            return false;
        }
    }

    private async Task<bool> TakeAsync(Parts work, int domainId, CancellationToken cancellationToken)
    {
        if (await work.Queue.TakeAsync(domainId, work.Rerun.Version, cancellationToken).ConfigureAwait(false) is not { } hold)
        {
            return false;
        }

        using var access = await work.Open(cancellationToken).ConfigureAwait(false);
        if (access.Resanitizer is not { } resanitizer)
        {
            _ = await work.Queue.ReturnAsync(hold, cancellationToken).ConfigureAwait(false);
            logger.RerunKeyUnread(hold.ItemId, domainId);
            return false;
        }

        await TreatAsync(work, resanitizer, hold, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>Reruns the held item and concludes it; a step that fails, or an occurrence outside the raw text, spends a try of it.</summary>
    private async Task TreatAsync(Parts work, ItemResanitizer resanitizer, RerunHold hold, CancellationToken cancellationToken)
    {
        try
        {
            var result = await resanitizer.ResanitizeAsync(hold, held => DecidedItemTexts.Rerun(work.Rerun, held), cancellationToken).ConfigureAwait(false);
            Report(hold, result);
        }
        catch (Exception failure) when (failure is SanitizationStepFailedException or ArgumentException)
        {
            logger.RerunFailed(hold.ItemId, hold.DomainId, failure.GetType().Name);
            if (await work.Queue.FailAsync(hold, cancellationToken).ConfigureAwait(false) == RerunOutcome.Refused)
            {
                logger.RerunRefused(hold.ItemId, hold.DomainId, RerunRefusal.Failed);
            }
        }
    }

    private void Report(RerunHold hold, RerunResult result)
    {
        if (result.Refusal is { } refusal)
        {
            logger.RerunRefused(hold.ItemId, hold.DomainId, refusal);
        }
        else
        {
            logger.RerunConcluded(hold.ItemId, hold.DomainId, result.Outcome);
        }
    }

    /// <summary>What the worker treats the queue with: the domains, the queue, the rerun of the steps in force and the content, opened for each item and let go at its end.</summary>
    internal sealed record Parts(DomainListing Domains, SanitizationReruns Queue, SanitizationRerun Rerun, Func<CancellationToken, Task<Access>> Open);

    /// <summary>The rerun of the held items over the content opened, none when the key was not read, and the reading of the key, let go with it.</summary>
    internal sealed record Access(ItemResanitizer? Resanitizer, IDisposable? Reading) : IDisposable
    {
        public void Dispose() => Reading?.Dispose();
    }
}
