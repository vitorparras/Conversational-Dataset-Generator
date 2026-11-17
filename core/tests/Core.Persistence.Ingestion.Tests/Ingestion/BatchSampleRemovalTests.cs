using Core.Persistence.Content;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// What the end of the review of the sample leaves (issue 822): a finding removes the whole material of the batch with the rejection,
/// and a review or an approval that is cancelled leaves the batch exactly as it was, open, not reviewed, and every item in place.
/// </summary>
public sealed class BatchSampleRemovalTests
{
    private const string Finding = "contact_not_treated";
    private const string Counted =
        "SELECT (SELECT count(*) FROM content.replication_mark) || ',' || (SELECT count(*) FROM content.replication_occurrence) || ',' || "
        + "(SELECT count(*) FROM content.reversal_map) || ',' || (SELECT count(*) FROM content.item_text)";

    private const string State = "SELECT state || ',' || coalesce(reviewed_by::text, '-') FROM content.ingestion_batch WHERE id = @id";
    private const string Waiting = "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock'";
    private const string Closings = "SELECT count(*) FROM public.custody_event WHERE step IN ('batch_reviewed', 'batch_closed')";

    private static readonly DateTimeOffset Said = new(2031, 5, 6, 9, 0, 0, TimeSpan.Zero);

    [DatabaseFact]
    public async Task ApprovalCancelledLeavesTheBatchOpenAndUnreviewed()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await DrawnAsync(world, 2);
        await CancelledWhileWaitingAsync(
            world, batch, token => new BatchApprovals(world.DataSource).ApproveAsync(new BatchApproval(IngestionWorld.Domain, batch, IngestionWorld.Person), token));

        Assert.Equal(("open,-", 0L), (await world.AsSuperuserAsync(State, ("id", batch)), await world.AsSuperuserAsync(Closings)));
    }

    [DatabaseFact]
    public async Task RejectionCancelledLeavesEveryItemInPlace()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await DrawnAsync(world, 3);
        await CancelledWhileWaitingAsync(world, batch, token => ReviewAsync(world, batch, Finding, token));

        Assert.Equal(("open,-", 3L), (await world.AsSuperuserAsync(State, ("id", batch)), await ItemsAsync(world, batch)));
    }

    [DatabaseFact]
    public async Task RejectionForFindingLeavesNoTraceOfTheItems()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await DrawnAsync(world, 3);

        var reviewed = await ReviewAsync(world, batch, Finding, TestContext.Current.CancellationToken);

        Assert.Equal((BatchReviewOutcome.Rejected, 0L), (reviewed.Outcome, await ItemsAsync(world, batch)));
    }

    [DatabaseFact]
    public async Task RejectionForFindingRemovesMarksAndOccurrences()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        var item = await IngestionWorld.AddItemAsync(world, batch);
        _ = await IngestionWorld.AddItemAsync(world, batch);
        _ = await IngestionWorld.AddMapAsync(world, batch, item);
        var occurrence = new StoredOccurrence(item, 0, new string('a', 64), new string('b', 64), 40, Said);
        var target = new ReplicationTarget(IngestionWorld.Domain, batch);
        await new ReplicationStore(world.DataSource).RecordAsync(target, [occurrence], [new StoredMark(item, 0, item, 0)], TestContext.Current.CancellationToken);
        var drawn = await new BatchReviews(world.DataSource, 3).DrawAsync(new BatchSampling(IngestionWorld.Domain, batch, IngestionWorld.Person), TestContext.Current.CancellationToken);
        Assert.Equal(BatchReviewOutcome.Drawn, drawn.Outcome);
        var before = await world.AsSuperuserAsync(Counted);

        var reviewed = await ReviewAsync(world, batch, Finding, TestContext.Current.CancellationToken);

        Assert.Equal((BatchReviewOutcome.Rejected, "1,1,1,2", "0,0,0,0"), (reviewed.Outcome, before, await world.AsSuperuserAsync(Counted)));
    }

    /// <summary>Cancels the step once it waits for the turn of the batch, which another transaction holds: the cancellation comes in the middle of the call.</summary>
    private static async Task CancelledWhileWaitingAsync(BatchesWorld world, long batch, Func<CancellationToken, Task> step)
    {
        var holder = await ContentTransaction.BeginAsync(world.DataSource, IngestionWorld.Domain, TestContext.Current.CancellationToken);
        await using (holder.ConfigureAwait(false))
        {
            var turn = holder.CreateCommand("SELECT public.take_batch_turn(@id)");
            await using (turn.ConfigureAwait(false))
            {
                _ = turn.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = batch });
                _ = await turn.ExecuteScalarAsync(TestContext.Current.CancellationToken);
            }

            using var cancelled = new CancellationTokenSource();
            var running = step(cancelled.Token);
            var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
            while ((long)(await world.AsSuperuserAsync(Waiting))! == 0)
            {
                Assert.True(DateTimeOffset.UtcNow < deadline, "The step did not wait for the turn of the batch.");
                await Task.Delay(50, TestContext.Current.CancellationToken);
            }

            await cancelled.CancelAsync();
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        }
    }

    private static Task<BatchReviewResult> ReviewAsync(BatchesWorld world, long batch, string? finding, CancellationToken token) =>
        new BatchReviews(world.DataSource, 3).ReviewAsync(new BatchReviewing(IngestionWorld.Domain, batch, IngestionWorld.Person, finding), token);

    private static async Task<object?> ItemsAsync(BatchesWorld world, long batch) =>
        await world.AsSuperuserAsync("SELECT count(*) FROM content.item_text WHERE batch_id = @id", ("id", batch));

    private static async Task<long> DrawnAsync(BatchesWorld world, int items)
    {
        var batch = await IngestionWorld.OpenAsync(world);
        for (var index = 0; index < items; index++)
        {
            _ = await IngestionWorld.AddItemAsync(world, batch);
        }

        var drawn = await new BatchReviews(world.DataSource, 3).DrawAsync(new BatchSampling(IngestionWorld.Domain, batch, IngestionWorld.Person), TestContext.Current.CancellationToken);
        Assert.Equal(BatchReviewOutcome.Drawn, drawn.Outcome);
        return batch;
    }
}
