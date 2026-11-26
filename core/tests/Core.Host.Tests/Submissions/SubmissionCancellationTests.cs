using Core.Host.Submissions;
using Core.Ingestion;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.Host.Tests.Submissions;

/// <summary>
/// A submission cancelled while its items were written (issue 775): the items are already in the batch when the request goes away, during the marking of the replicated content;
/// the batch is rejected as submission_cancelled under a token of its own, with no item and no mark, and the same file sent again opens a new batch, since only an accepted one repeats.
/// </summary>
public sealed class SubmissionCancellationTests
{
    private static readonly int[] Granted = [SubmissionWorld.Domain];

    [DatabaseFact]
    public async Task CancelledWhileWritingRejectsTheBatchAsSubmissionCancelled()
    {
        await using var world = await SubmissionWorld.CreateAsync();
        using var cancelling = new CancellationTokenSource();

        var outcome = await CancelledBy(world, cancelling).SubmitAsync(SubmissionWorld.Request(), cancelling.Token);
        var detail = await world.Service.DetailAsync(Granted, outcome.Batch!.Value, TestContext.Current.CancellationToken);

        Assert.Equal(
            (SubmissionKind.Unavailable, SubmissionStoring.SubmissionCancelled, BatchState.Rejected, SubmissionStoring.SubmissionCancelled),
            (outcome.Kind, outcome.Code, detail!.State, detail.Reason));
    }

    [DatabaseFact]
    public async Task CancelledWhileWritingLeavesNoItems()
    {
        await using var world = await SubmissionWorld.CreateAsync();
        using var cancelling = new CancellationTokenSource();

        _ = await CancelledBy(world, cancelling).SubmitAsync(SubmissionWorld.Request(), cancelling.Token);

        Assert.Equal(
            (true, 0L, 0L, 0L),
            (cancelling.IsCancellationRequested, await world.ItemRowsAsync(), await world.MarkRowsAsync(), await world.OccurrenceRowsAsync()));
    }

    [DatabaseFact]
    public async Task ResubmittingAfterSubmissionCancelledOpensANewBatch()
    {
        await using var world = await SubmissionWorld.CreateAsync();
        using var cancelling = new CancellationTokenSource();
        var cancelled = await CancelledBy(world, cancelling).SubmitAsync(SubmissionWorld.Request(), cancelling.Token);

        var again = await world.SubmitAsync(SubmissionWorld.Request());

        Assert.NotEqual(cancelled.Batch, again.Batch);
        Assert.Equal((SubmissionKind.Done, SubmissionState.Accepted, 2L, 2L), (again.Kind, again.State, await world.ItemRowsAsync(), await world.BatchCountAsync()));
    }

    /// <summary>A service over the world whose marking of the replicated content cancels the submission, after every item of the conversation was written.</summary>
    private static SubmissionService CancelledBy(SubmissionWorld world, CancellationTokenSource cancelling)
    {
        var conditions = OriginConditions.Of([SubmissionWorld.Strictest, "zqx_media", SubmissionWorld.Loosest]);
        var rule = new ReplicationRule(minLength: 10, minSenders: 2);
        var intake = SubmissionWorld.Intake(1_000_000, conditions, world.Content, world.DataSource, rule, new CancellingBoundaries(cancelling));
        return new SubmissionService(intake, world.Batches, world.DataSource, NullLogger.Instance);
    }
}
