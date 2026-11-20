using System.Data;
using Core.Persistence.Custody;
using Npgsql;

namespace Core.Persistence.Content.Rerun;

/// <summary>
/// The queue of the rerun of the sanitization (0064): a request queues the items of the accepted batches of another version, with its event in the same transaction, and closes as superseded what
/// was left of a request to another version; a worker holds one item at a time, for <see cref="Hold"/>, and an item spends a try when its hold expires or it fails, until the last one refuses it.
/// </summary>
public sealed class SanitizationReruns(NpgsqlDataSource dataSource, TimeProvider time)
{
    /// <summary>How long a worker holds an item before another may take it.</summary>
    public static TimeSpan Hold { get; } = TimeSpan.FromMinutes(5);

    /// <summary>How many tries an item has; the last one that fails, or whose hold expires, refuses it.</summary>
    public static int Tries { get; } = 3;

    /// <summary>Queues the items the request reruns, or answers the open request when none is left to queue.</summary>
    public Task<RerunRequestResult> RequestAsync(RerunRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return RerunRequesting.RequestAsync((dataSource, time), request, cancellationToken);
    }

    /// <summary>Holds one item of a request to the version, queued or whose hold expired, after refusing those whose last hold expired; none when there is none.</summary>
    public Task<RerunHold?> TakeAsync(int domainId, string version, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        return TakingAsync(domainId, version, cancellationToken);
    }

    /// <summary>Gives the held item back to the queue without spending a try; false when the hold expired and is no longer this one.</summary>
    public Task<bool> ReturnAsync(RerunHold hold, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hold);
        return RerunCommands.ReturnAsync(dataSource, hold, time.GetUtcNow(), cancellationToken);
    }

    /// <summary>Spends a try of the held item: <see cref="RerunOutcome.Failed"/> when it goes back to the queue, <see cref="RerunOutcome.Refused"/> at the last try.</summary>
    public Task<RerunOutcome> FailAsync(RerunHold hold, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hold);
        return RerunCommands.FailAsync(dataSource, hold, time.GetUtcNow(), cancellationToken);
    }

    /// <summary>The items of the accepted batches of the domain by version, and the open request.</summary>
    public async Task<RerunSituation> SituationAsync(int domainId, CancellationToken cancellationToken)
    {
        var transaction = await ContentTransaction.BeginAsync(dataSource, domainId, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var scope = transaction.AsCustodyScope();
            var versions = await RerunReading.RowsAsync(scope, (ContentQueries.ReadRerunSituation, _ => { }), VersionOf, cancellationToken).ConfigureAwait(false);
            return new RerunSituation(versions, await RerunReading.OpenAsync(scope, cancellationToken).ConfigureAwait(false));
        }
    }

    /// <summary>Where the request of the domain stands; none when the domain has no such request.</summary>
    public Task<RerunProgress?> ProgressAsync(int domainId, long rerunId, CancellationToken cancellationToken) => RerunReading.ProgressAsync(dataSource, domainId, rerunId, cancellationToken);

    private static VersionItems VersionOf(IDataRecord row) => new(row.IsDBNull(0) ? null : row.GetString(0), row.GetInt64(1));

    // The columns of the hold: entry, request, item, tries and the moment it expires.
    private static RerunHold HoldOf(NpgsqlDataReader row, int domainId, string version)
    {
        const int Item = 2;
        const int TriesSpent = 3;
        const int Until = 4;
        return new RerunHold(domainId, row.GetInt64(0), row.GetInt64(1), row.GetInt64(Item), version, row.GetFieldValue<DateTimeOffset>(Until), row.GetInt32(TriesSpent));
    }

    private async Task<RerunHold?> TakingAsync(int domainId, string version, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var transaction = await ContentTransaction.BeginAsync(dataSource, domainId, IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var scope = transaction.AsCustodyScope();
            var exhausted = await RerunCommands.ConcludedAsync(scope, (ContentQueries.CloseRerunExhausted, RerunCommands.Over(version, now)), version, cancellationToken).ConfigureAwait(false);
            if (await RerunCommands.RecordEachAsync(scope, exhausted, CustodyReason.SanitizationRefused, cancellationToken).ConfigureAwait(false) is { Outcome: not CustodyOutcome.Appended })
            {
                return null;
            }

            var held = await RerunReading.RowsAsync(scope, (ContentQueries.CloseRerunHold, RerunCommands.Over(version, now)), row => HoldOf(row, domainId, version), cancellationToken)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return held.Count == 0 ? null : held[0];
        }
    }
}
