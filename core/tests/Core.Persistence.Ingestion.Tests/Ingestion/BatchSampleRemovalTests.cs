using Core.Persistence.Content;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;

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
    private const string Closings = "SELECT count(*) FROM public.custody_event WHERE step IN ('batch_reviewed', 'batch_closed')";

    private static readonly DateTimeOffset Said = new(2031, 5, 6, 9, 0, 0, TimeSpan.Zero);

    [DatabaseFact]
    public async Task ApprovalCancelledLeavesTheBatchOpenAndUnreviewed()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await DrawnAsync(world, 2);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new BatchApprovals(world.DataSource).ApproveAsync(new BatchApproval(IngestionWorld.Domain, batch, IngestionWorld.Person), cancelled.Token));

        Assert.Equal(("open,-", 0L), (await world.AsSuperuserAsync(State, ("id", batch)), await world.AsSuperuserAsync(Closings)));
    }

    [DatabaseFact]
    public async Task RejectionCancelledLeavesEveryItemInPlace()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await DrawnAsync(world, 3);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ReviewAsync(world, batch, Finding, cancelled.Token));

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
        var batch = await DrawnAsync(world, 2);
        var item = (await new BatchReviews(world.DataSource, 3).ReadAsync(IngestionWorld.Domain, batch, TestContext.Current.CancellationToken))!.Sample![0];
        _ = await IngestionWorld.AddMapAsync(world, batch, item);
        var occurrence = new StoredOccurrence(item, 0, new string('a', 64), new string('b', 64), 40, Said);
        var target = new ReplicationTarget(IngestionWorld.Domain, batch);
        await new ReplicationStore(world.DataSource).RecordAsync(target, [occurrence], [new StoredMark(item, 0, item, 0)], TestContext.Current.CancellationToken);
        var before = await world.AsSuperuserAsync(Counted);

        var reviewed = await ReviewAsync(world, batch, Finding, TestContext.Current.CancellationToken);

        Assert.Equal((BatchReviewOutcome.Rejected, "1,1,1,2", "0,0,0,0"), (reviewed.Outcome, before, await world.AsSuperuserAsync(Counted)));
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
