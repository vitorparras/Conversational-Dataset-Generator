using System.Data;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Custody.Chain.Append;
using Npgsql;
using NpgsqlTypes;
using static Core.Persistence.Content.Rerun.RerunRows;

namespace Core.Persistence.Content.Rerun;

/// <summary>
/// The queue of the rerun of the sanitization (0064): a request queues the items of the accepted batches of another version, with its event in the same transaction, and closes as superseded what
/// was left of a request to another version; a worker holds one item at a time, for <see cref="Hold"/>, and an item spends a try when its hold expires or it fails, until the last one refuses it.
/// </summary>
public sealed class SanitizationReruns
{
    private const int Attempts = 3;

    private readonly NpgsqlDataSource dataSource;
    private readonly TimeProvider time;

    /// <summary>The queue over the database, at the moments the clock gives.</summary>
    /// <param name="dataSource">The source of the connections, with the application's credential.</param>
    /// <param name="time">The clock of the requests, the holds and the conclusions.</param>
    public SanitizationReruns(NpgsqlDataSource dataSource, TimeProvider time)
    {
        this.dataSource = dataSource;
        this.time = time;
    }

    /// <summary>How long a worker holds an item before another may take it.</summary>
    public static TimeSpan Hold { get; } = TimeSpan.FromMinutes(5);

    /// <summary>How many tries an item has; the last one that fails, or whose hold expires, refuses it.</summary>
    public static int Tries { get; } = 3;

    /// <summary>Queues the items the request reruns, or answers the open request when none is left to queue; a conflict is tried again, up to three times.</summary>
    public async Task<RerunResult> RequestAsync(RerunRequest request, CancellationToken cancellationToken)
    {
        var tries = 0;
        (RerunResult Result, CustodyResult? Recorded) answer;
        do
        {
            answer = await RequestingAsync(request, cancellationToken).ConfigureAwait(false);
        }
        while (answer.Recorded?.Outcome == CustodyOutcome.Conflict && ++tries < Attempts);

        return answer.Recorded is null or { Outcome: CustodyOutcome.Appended } ? answer.Result : new RerunResult(RerunOutcome.NotRecorded);
    }

    /// <summary>Holds one item of a request to the version, queued or whose hold expired, after refusing those whose last hold expired; none when there is none.</summary>
    public async Task<RerunHold?> TakeAsync(int domainId, string version, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var transaction = await ContentTransaction.BeginAsync(dataSource, domainId, IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var scope = transaction.AsCustodyScope();
            var exhausted = await RowsAsync(scope, (ContentQueries.CloseRerunExhausted, Over(version, now)), row => (row.GetInt64(0), version), cancellationToken).ConfigureAwait(false);
            if (await RecordEachAsync(scope, exhausted, CustodyReason.SanitizationRefused, cancellationToken).ConfigureAwait(false) is { Outcome: not CustodyOutcome.Appended })
            {
                return null;
            }

            var held = await RowsAsync(scope, (ContentQueries.CloseRerunHold, Over(version, now)), row =>
            {
                var at = 0;
                return new RerunHold(domainId, row.GetInt64(at++), row.GetInt64(at++), row.GetInt64(at++), version, row.GetFieldValue<DateTimeOffset>(at++), row.GetInt32(at));
            }, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return held.Count == 0 ? null : held[0];
        }
    }

    /// <summary>Gives the held item back to the queue without spending a try: <see cref="RerunOutcome.Returned"/>, or <see cref="RerunOutcome.NotHeld"/> when the hold expired.</summary>
    public Task<RerunOutcome> ReturnAsync(RerunHold hold, CancellationToken cancellationToken) =>
        SpendAsync(dataSource, hold, (ContentQueries.CloseRerunReturned, RerunOutcome.Returned, time.GetUtcNow()), cancellationToken);

    /// <summary>Spends a try of the held item: <see cref="RerunOutcome.Failed"/> when it goes back to the queue, <see cref="RerunOutcome.Refused"/> at the last try.</summary>
    public Task<RerunOutcome> FailAsync(RerunHold hold, CancellationToken cancellationToken) =>
        SpendAsync(dataSource, hold, (ContentQueries.CloseRerunFailed, RerunOutcome.Failed, time.GetUtcNow()), cancellationToken);

    /// <summary>The items of the accepted batches of the domain by version, and the open request.</summary>
    public Task<RerunSituation> SituationAsync(int domainId, CancellationToken cancellationToken) => InDomainAsync(domainId, async scope =>
    {
        var versions = await RowsAsync(
            scope, (ContentQueries.ReadRerunSituation, []), row => new VersionItems(row.IsDBNull(0) ? null : row.GetString(0), row.GetInt64(1), row.GetInt64(2)), cancellationToken)
            .ConfigureAwait(false);
        var open = await RowsAsync(scope, (ContentQueries.ReadRerunOpen, []), row => (Id: row.GetInt64(0), Version: row.GetString(1)), cancellationToken).ConfigureAwait(false);
        return open.Count == 0 ? new RerunSituation(versions, null, null) : new RerunSituation(versions, open[0].Id, open[0].Version);
    }, cancellationToken);

    /// <summary>Where the request of the domain stands; none when the domain has no such request.</summary>
    public Task<RerunProgress?> ProgressAsync(int domainId, long rerunId, CancellationToken cancellationToken) => InDomainAsync(domainId, async scope =>
    {
        Bound[] rerun = [new("rerun", NpgsqlDbType.Bigint, rerunId)];
        var progress = await RowsAsync(scope, (ContentQueries.ReadRerunProgress, rerun), row =>
        {
            var at = 0;
            return new RerunProgress(
                row.GetString(at++), row.GetFieldValue<DateTimeOffset>(at++), row.GetInt64(at++), row.GetInt64(at++), row.GetInt64(at++), row.GetInt64(at++), row.GetInt64(at++),
                row.GetInt64(at++), row.IsDBNull(at) ? null : row.GetFieldValue<DateTimeOffset>(at), []);
        }, cancellationToken).ConfigureAwait(false);
        var refusals = await RowsAsync(
            scope, (ContentQueries.ReadRerunRefusals, rerun), row => new RerunItemRefusal(row.GetInt64(0), row.GetInt64(1), RefusalOf(row.GetString(row.FieldCount - 1))), cancellationToken)
            .ConfigureAwait(false);
        return progress.Count == 0 ? null : progress[0] with { Refusals = refusals };
    }, cancellationToken);

    private async Task<T> InDomainAsync<T>(int domainId, Func<CustodyScope, Task<T>> read, CancellationToken cancellationToken)
    {
        var transaction = await ContentTransaction.BeginAsync(dataSource, domainId, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            return await read(transaction.AsCustodyScope()).ConfigureAwait(false);
        }
    }

    private async Task<(RerunResult, CustodyResult?)> RequestingAsync(RerunRequest request, CancellationToken cancellationToken)
    {
        var answer = new RerunResult(RerunOutcome.NotRecorded);
        try
        {
            var recorded = await CustodyTransaction.RunAsync(dataSource, async scope =>
            {
                (answer, var appended) = await RequestInAsync(scope, (request, time.GetUtcNow()), cancellationToken).ConfigureAwait(false);
                return appended;
            }, cancellationToken).ConfigureAwait(false);
            return (answer, recorded);
        }
        catch (PostgresException failed) when (failed.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // Another request queued the same item first; the next attempt finds it in the open request.
            return (answer, CustodyRefusals.Conflict());
        }
    }

    private static async Task<(RerunResult, CustodyResult?)> RequestInAsync(CustodyScope scope, (RerunRequest Request, DateTimeOffset Now) asked, CancellationToken cancellationToken)
    {
        var (request, now) = asked;
        await DeclareAsync(scope, request.DomainId, cancellationToken).ConfigureAwait(false);
        var superseded = await RowsAsync(scope, (ContentQueries.CloseRerunSuperseded, Over(request.Version, now)), row => (row.GetInt64(0), row.GetString(1)), cancellationToken).ConfigureAwait(false);
        var recorded = await RecordEachAsync(scope, superseded, CustodyReason.SanitizationSuperseded, cancellationToken).ConfigureAwait(false);
        if (recorded is { Outcome: not CustodyOutcome.Appended })
        {
            return (new RerunResult(RerunOutcome.NotRecorded), recorded);
        }

        var pending = await RowsAsync(scope, (ContentQueries.ReadRerunPending, Over(request.Version, now)), row => row.GetInt64(0), cancellationToken).ConfigureAwait(false);
        var open = await RowsAsync(scope, (ContentQueries.ReadRerunOpen, []), row => row.GetInt64(0), cancellationToken).ConfigureAwait(false);
        if (pending.Count == 0)
        {
            var answer = open.Count == 0 ? new RerunResult(RerunOutcome.NothingToRerun) : new RerunResult(RerunOutcome.AlreadyOpen, open[0]);
            return (answer, recorded is null ? null : await CustodyAppend.CommitIfAppendedAsync(scope, recorded, cancellationToken).ConfigureAwait(false));
        }

        // The requests to another version were just superseded, so an open one reruns to this version: the new items join it, and the trail says who queued them.
        Bound[] asking =
            [.. Over(request.Version, now), new("domain", NpgsqlDbType.Integer, request.DomainId), new("steps", NpgsqlDbType.Text, request.Steps),
                new("person", NpgsqlDbType.Bigint, request.PersonId), new("rerun", NpgsqlDbType.Bigint, open.Count == 0 ? null : open[0])];
        var rerunId = (long)(await ScalarAsync(scope, (ContentQueries.InsertSanitizationRerun, asking), cancellationToken).ConfigureAwait(false))!;
        var appended = await CustodyAppend.AppendAsync(scope, CustodyEvent.SanitizationRerun(request.PersonId, rerunId, request.Version), cancellationToken).ConfigureAwait(false);
        var outcome = open.Count == 0 ? RerunOutcome.Queued : RerunOutcome.AlreadyOpen;
        return (new RerunResult(outcome, rerunId), await CustodyAppend.CommitIfAppendedAsync(scope, appended, cancellationToken).ConfigureAwait(false));
    }
}
