using Core.Host.Tests.Submissions;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;

namespace Core.Host.Tests;

/// <summary>
/// What the desk of the review answers before any write, over a database (issue 837): the state of the batch is refused by the one rule the database takes,
/// so a batch that does not exist is not found at every step, and not "not sampled" as the desk once decided apart from the rule.
/// </summary>
[Trait("Category", "Database")]
public sealed class SampleReviewDeskTests
{
    private const string Kind = "endereco";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [DatabaseFact]
    public async Task ABatchThatDoesNotExistIsNotFoundAtEveryStepAndInTheView()
    {
        await using var world = await SampleReviewWorld.StartAsync();
        var missing = Entry(world.Batch + 1000);

        var answers = new[]
        {
            await world.Desk.MarkAsync(missing, 1, Token),
            await world.Desk.RejectAsync(missing, (1, Kind, world.Curator), Token),
            await world.Desk.ApproveAsync(missing, world.Curator, Token),
            (await world.Desk.ViewAsync(missing, "dominio", world.Curator, Token)).Outcome,
        };

        Assert.All(answers, answer => Assert.Equal(BatchReviewOutcome.NotFound, answer));
    }

    [DatabaseFact]
    public async Task AnOpenBatchWithoutDrawIsNotSampledAtEveryStepAndInTheView()
    {
        await using var world = await SampleReviewWorld.StartAsync();
        var open = Entry(world.Batch);

        var answers = new[]
        {
            await world.Desk.MarkAsync(open, 1, Token),
            await world.Desk.RejectAsync(open, (1, Kind, world.Curator), Token),
            await world.Desk.ApproveAsync(open, world.Curator, Token),
            (await world.Desk.ViewAsync(open, "dominio", world.Curator, Token)).Outcome,
        };

        Assert.All(answers, answer => Assert.Equal(BatchReviewOutcome.NotSampled, answer));
    }

    [DatabaseFact]
    public async Task ABatchThatWasReviewedIsAlreadyReviewedAtEveryStepAndItsViewStillShowsTheSample()
    {
        await using var world = await SampleReviewWorld.StartAsync();
        await world.DrawAndReviewAsync(reviewed: 1);
        var batch = Entry(world.Batch);
        Assert.Equal(BatchReviewOutcome.Rejected, await world.Desk.RejectAsync(batch, (1, Kind, world.Curator), Token));

        var steps = new[]
        {
            await world.Desk.MarkAsync(batch, 2, Token),
            await world.Desk.RejectAsync(batch, (1, Kind, world.Curator), Token),
            await world.Desk.ApproveAsync(batch, world.Curator, Token),
        };
        var (outcome, screen) = await world.Desk.ViewAsync(batch, "dominio", world.Curator, Token);

        Assert.All(steps, step => Assert.Equal(BatchReviewOutcome.AlreadyReviewed, step));
        Assert.Equal((BatchReviewOutcome.Drawn, true), (outcome, screen is not null));
    }

    [DatabaseFact]
    public async Task AStepOfTheSampleThatWaitsGoesOnWhenTheOrderOfThePlacesAllowsIt()
    {
        await using var world = await SampleReviewWorld.StartAsync();
        await world.DrawAndReviewAsync(reviewed: 0);
        var batch = Entry(world.Batch);

        var skipped = await world.Desk.MarkAsync(batch, 2, Token);
        var next = await world.Desk.MarkAsync(batch, 1, Token);
        var early = await world.Desk.ApproveAsync(batch, world.Curator, Token);

        Assert.Equal((BatchReviewOutcome.PlaceNotNext, BatchReviewOutcome.Marked, BatchReviewOutcome.ReviewIncomplete), (skipped, next, early));
    }

    private static BatchEntry Entry(long batch) =>
        new() { Id = batch, DomainId = SubmissionWorld.Domain, State = BatchState.Open, SubmittedBy = SampleReviewWorld.Annotator, SubmittedAt = DateTimeOffset.UnixEpoch };
}
