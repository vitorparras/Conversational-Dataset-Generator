using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// The review and the approval of a sample that meet a writer of the chain not yet committed (issue 822): they are repeated, so that the collision costs the caller nothing,
/// and what they write is written once, with the batch in the state the review leaves it.
/// </summary>
public sealed class SampleCollisionTests
{
    private const string State = "SELECT state || ',' || coalesce(reviewed_by::text, '-') FROM content.ingestion_batch WHERE id = @id";
    private const string Events = "SELECT string_agg(step, ',' ORDER BY sequence) FROM public.custody_event WHERE step IN ('batch_reviewed', 'batch_closed')";

    [DatabaseFact]
    public async Task ACleanReviewThatMeetsAWriterNotYetCommittedIsRepeatedAndRecordedOnce()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await DrawnAsync(world);

        var reviewed = await ChainContention.WhilePendingAsync(
            world,
            () => new BatchReviews(world.DataSource, 2).ReviewAsync(new BatchReviewing(IngestionWorld.Domain, batch, IngestionWorld.Person), TestContext.Current.CancellationToken));

        Assert.Equal(
            (BatchReviewOutcome.Reviewed, false, $"open,{IngestionWorld.Person}", "batch_reviewed"),
            (reviewed.Outcome, reviewed.Collided, (string)(await world.AsSuperuserAsync(State, ("id", batch)))!, (string)(await world.AsSuperuserAsync(Events))!));
    }

    [DatabaseFact]
    public async Task AReviewWithAFindingThatMeetsAWriterNotYetCommittedIsRepeatedAndRejectsTheBatchOnce()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await DrawnAsync(world);

        var reviewed = await ChainContention.WhilePendingAsync(
            world,
            () => new BatchReviews(world.DataSource, 2).ReviewAsync(
                new BatchReviewing(IngestionWorld.Domain, batch, IngestionWorld.Person, "contact_not_treated"), TestContext.Current.CancellationToken));

        Assert.Equal(
            (BatchReviewOutcome.Rejected, false, $"rejected,{IngestionWorld.Person}", "batch_reviewed,batch_closed"),
            (reviewed.Outcome, reviewed.Collided, (string)(await world.AsSuperuserAsync(State, ("id", batch)))!, (string)(await world.AsSuperuserAsync(Events))!));
    }

    [DatabaseFact]
    public async Task AnApprovalThatMeetsAWriterNotYetCommittedIsRepeatedAndWritesTheReviewAndTheAcceptanceOnce()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await DrawnAsync(world);

        var approved = await ChainContention.WhilePendingAsync(
            world,
            () => new BatchApprovals(world.DataSource).ApproveAsync(new BatchApproval(IngestionWorld.Domain, batch, IngestionWorld.Person), TestContext.Current.CancellationToken));

        Assert.Equal(
            (BatchReviewOutcome.Approved, false, $"accepted,{IngestionWorld.Person}", "batch_reviewed,batch_closed"),
            (approved.Outcome, approved.Collided, (string)(await world.AsSuperuserAsync(State, ("id", batch)))!, (string)(await world.AsSuperuserAsync(Events))!));
    }

    private static async Task<long> DrawnAsync(BatchesWorld world)
    {
        var batch = await IngestionWorld.OpenAsync(world);
        _ = await IngestionWorld.AddItemAsync(world, batch);
        _ = await IngestionWorld.AddItemAsync(world, batch);
        var drawn = await new BatchReviews(world.DataSource, 2).DrawAsync(new BatchSampling(IngestionWorld.Domain, batch, IngestionWorld.Person), TestContext.Current.CancellationToken);
        Assert.Equal(BatchReviewOutcome.Drawn, drawn.Outcome);
        return batch;
    }
}
