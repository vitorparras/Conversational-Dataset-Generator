using Core.Host.Settings;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Verification;
using Core.Persistence.People;
using Core.Persistence.Settings;
using Core.Platform.Configuration;
using Npgsql;

namespace Core.Host;

/// <summary>
/// Records in the chain the approved changes of configuration that will never be applied (issue 748; decision 0053): at the start and at each interval it goes through the approved changes not yet
/// in force with the same judgment the applier makes, and for a definitive reason writes one event of the system with the code of the reason. A reason that may pass, such as a chain that does not
/// close, leaves the change pending. A change whose value is already in force is not judged at all: it belongs to the service that
/// marks it in force, and the two must never contradict each other. The applier only reads, so it is the core that writes; the event is
/// once per change, by a unique index.
/// </summary>
internal sealed class ConfigurationRefusals : BackgroundService
{
    internal static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly ApprovedChanges? changes;
    private readonly ApplicationFactsReader? facts;
    private readonly IReadOnlyDictionary<string, string> inForce;
    private readonly ILoggerFactory loggers;

    /// <summary>The service as the host builds it: over the data source of the startup container, which a host with no database does not have.</summary>
    internal ConfigurationRefusals(IServiceProvider host)
        : this(host.GetRequiredService<ServiceProvider>().GetService<NpgsqlDataSource>(), host)
    {
    }

    private ConfigurationRefusals(NpgsqlDataSource? source, IServiceProvider host)
        : this(
              source is null ? null : new ApprovedChanges(source),
              source is null ? null : new ApplicationFactsReader(new CustodyVerifier(source, TimeProvider.System), new PersonRegistry(source)),
              host.GetRequiredService<EffectiveConfiguration>().Entries,
              host.GetRequiredService<ILoggerFactory>())
    {
    }

    /// <summary>The service over the register and the reader of the facts, the keys in force and the log of its caller.</summary>
    internal ConfigurationRefusals(ApprovedChanges? changes, ApplicationFactsReader? facts, IReadOnlyList<EffectiveEntry> entries, ILoggerFactory loggers)
    {
        this.changes = changes;
        this.facts = facts;
        inForce = entries.Where(entry => entry.Value is not null).ToDictionary(entry => entry.Key, entry => entry.Value!, StringComparer.Ordinal);
        this.loggers = loggers;
    }

    /// <summary>Records every definitive refusal among the approved changes not yet in force; the number recorded.</summary>
    /// <param name="cancellationToken">Cancels the round.</param>
    internal async Task<int> RecordAsync(CancellationToken cancellationToken)
    {
        var logger = loggers.CreateLogger<ConfigurationRefusals>();
        var recorded = 0;
        try
        {
            foreach (var change in changes is null ? new List<ApprovedChange>() : await changes.PendingAsync(cancellationToken).ConfigureAwait(false))
            {
                recorded += await JudgeAsync(change, logger, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception failure) when (ServingFailure.CanReport(failure) && !cancellationToken.IsCancellationRequested)
        {
            logger.ConfigurationRefusalsFailed(failure.GetType().Name);
        }

        return recorded;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (true)
            {
                _ = await RecordAsync(stoppingToken).ConfigureAwait(false);
                await Task.Delay(Interval, TimeProvider.System, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping: ending is not a failure.
        }
    }

    private async Task<int> JudgeAsync(ApprovedChange change, ILogger logger, CancellationToken cancellationToken)
    {
        if (inForce.TryGetValue(change.Key, out var current) && current == change.Value)
        {
            return 0;
        }

        var refusal = ApplicationJudge.Judge(change, await facts!.ReadAsync(change, cancellationToken).ConfigureAwait(false), inForce);
        if (refusal == ApplicationRefusal.None || !refusal.IsDefinitive())
        {
            return 0;
        }

        var recorded = await changes!.RecordNotAppliedAsync(change, refusal.CodeOf(), cancellationToken).ConfigureAwait(false);
        if (recorded.Outcome != CustodyOutcome.Appended)
        {
            logger.ChangeWillNotBeAppliedNotRecorded(change.Id, refusal, recorded.Outcome);
            return 0;
        }

        logger.ChangeWillNotBeApplied(change.Id, refusal, change.ApprovalSequence, recorded.Sequence);
        return 1;
    }
}
