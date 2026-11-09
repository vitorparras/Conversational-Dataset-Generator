using Core.Persistence.Custody;
using Core.Persistence.Settings;
using Core.Platform.Configuration;
using Npgsql;

namespace Core.Host;

/// <summary>
/// Marks the approved changes of configuration as in force (decision 0053): when the core starts and a key already has the value an approved change asked for, one event of the system in the
/// chain says so, pointing at the approval. A change whose value is not the one in force is left alone, for the applier or for the next start. A failure writes nothing and is told in the log.
/// </summary>
internal sealed class ConfigurationInForce : BackgroundService
{
    private readonly ApprovedChanges? changes;
    private readonly IReadOnlyList<EffectiveEntry> entries;
    private readonly ILoggerFactory loggers;

    /// <summary>The service as the host builds it: over the data source of the startup container, which a host with no database does not have.</summary>
    internal ConfigurationInForce(IServiceProvider host)
        : this(ChangesOf(host), host.GetRequiredService<EffectiveConfiguration>().Entries, host.GetRequiredService<ILoggerFactory>())
    {
    }

    /// <summary>The service over the register it marks in, the keys in force and the log of its caller.</summary>
    internal ConfigurationInForce(ApprovedChanges? changes, IReadOnlyList<EffectiveEntry> entries, ILoggerFactory loggers)
    {
        this.changes = changes;
        this.entries = entries;
        this.loggers = loggers;
    }

    /// <summary>Marks every approved change whose value is in force; the number marked.</summary>
    /// <param name="cancellationToken">Cancels the marking.</param>
    internal async Task<int> RecordAsync(CancellationToken cancellationToken)
    {
        var logger = loggers.CreateLogger<ConfigurationInForce>();
        var marked = 0;
        try
        {
            foreach (var change in await PendingAsync(cancellationToken).ConfigureAwait(false))
            {
                marked += await MarkAsync(change, logger, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception failure) when (ServingFailure.CanReport(failure) && !cancellationToken.IsCancellationRequested)
        {
            logger.ConfigurationInForceFailed(failure.GetType().Name);
        }

        return marked;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _ = await RecordAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping: ending is not a failure.
        }
    }

    private static ApprovedChanges? ChangesOf(IServiceProvider host) =>
        host.GetRequiredService<ServiceProvider>().GetService<NpgsqlDataSource>() is { } source ? new ApprovedChanges(source) : null;

    private async Task<IReadOnlyList<ApprovedChange>> PendingAsync(CancellationToken cancellationToken) =>
        changes is null ? new List<ApprovedChange>() : await changes.PendingAsync(cancellationToken).ConfigureAwait(false);

    private async Task<int> MarkAsync(ApprovedChange change, ILogger logger, CancellationToken cancellationToken)
    {
        if (!entries.Any(entry => entry.Key == change.Key && entry.Value == change.Value))
        {
            return 0;
        }

        var recorded = await changes!.RecordInForceAsync(change, cancellationToken).ConfigureAwait(false);
        if (recorded.Outcome != CustodyOutcome.Appended)
        {
            logger.ChangeInForceNotRecorded(change.Id, recorded.Outcome);
            return 0;
        }

        logger.ChangeInForce(change.Id, change.ApprovalSequence, recorded.Sequence);
        return 1;
    }
}
