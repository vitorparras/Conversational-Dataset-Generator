using Core.Persistence.ServerGuarantees;
using Npgsql;

namespace Core.Host;

/// <summary>
/// Compares, every interval, what the server's audit log says it saw with what the chain recorded, from where the last comparison ended to a
/// moment short of now, so that the log has caught up with what it is asked about. A difference that is new is a warning in the log; one that
/// is known is only counted. A comparison that cannot run, such as one with no database to ask, leaves its interval for the next one.
/// </summary>
internal sealed class AuditComparisonService : BackgroundService
{
    private static readonly TimeSpan LogLag = TimeSpan.FromMinutes(1);

    private readonly AuditComparisonPlan plan;
    private readonly AuditComparisonState state;
    private readonly TimeProvider clock;

    /// <summary>The service runs the plan on the clock given and tells the state what each run found.</summary>
    internal AuditComparisonService(AuditComparisonPlan plan, AuditComparisonState state, TimeProvider clock)
    {
        this.plan = plan;
        this.state = state;
        this.clock = clock;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var from = clock.GetUtcNow() - LogLag;
        using var timer = new PeriodicTimer(plan.Interval, clock);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            from = await CompareOnceAsync(from, stoppingToken).ConfigureAwait(false);
        }
    }

    /// <summary>Compares from the instant given and returns where the next comparison starts: the end of this one, or the same start when it could not run.</summary>
    private async Task<DateTimeOffset> CompareOnceAsync(DateTimeOffset from, CancellationToken cancellationToken)
    {
        var to = clock.GetUtcNow() - LogLag;
        if (to <= from)
        {
            return from;
        }

        try
        {
            var found = await plan.Compare(from, to, cancellationToken).ConfigureAwait(false);
            Report(found);
            return to;
        }
        catch (Exception failure) when (CannotRun(failure))
        {
            _ = state.Record(new AuditComparisonStatus(AuditComparisonResult.Failed, clock.GetUtcNow(), 0, 0, []));
            plan.Log.ComparisonFailed(failure.GetType().FullName ?? failure.GetType().Name);
            return from;
        }
    }

    /// <summary>The failures of reading the log or asking the database, which the next interval may find gone; any other is a defect and stops the host.</summary>
    private static bool CannotRun(Exception failure) => failure is NpgsqlException or IOException or UnauthorizedAccessException or InvalidDataException;

    private static string Detail(AuditDifference difference) =>
        $"comandos {string.Join('|', difference.Commands)}, visto {difference.Seen}, gravado {difference.Recorded}, passos {string.Join('|', difference.Steps)}";

    private void Report(AuditCompared compared)
    {
        var found = compared.Differences;
        var fresh = found.Where(difference => difference.KnownGap is null).ToArray();
        var result = fresh.Length > 0 ? AuditComparisonResult.Different : AuditComparisonResult.Agreed;
        var unknown = state.Record(new AuditComparisonStatus(result, clock.GetUtcNow(), fresh.Length, found.Count - fresh.Length, compared.CutOffFiles));
        foreach (var file in unknown)
        {
            plan.Log.CutOffFile(file);
        }

        foreach (var difference in fresh)
        {
            plan.Log.NewDifference(difference.Role, difference.Table, Detail(difference));
        }
    }
}
