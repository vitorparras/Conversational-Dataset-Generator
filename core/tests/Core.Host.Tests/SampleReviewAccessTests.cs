using System.Net;
using Core.Host.Tests.Submissions;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;

namespace Core.Host.Tests;

/// <summary>
/// Who may take each step of the review of the sample (issue 822) and what happens when the database does not answer: a person that is not of the curation, or of a domain not granted,
/// is turned back as if the batch did not exist, and a step the database could not take leaves the batch as it was.
/// </summary>
[Trait("Category", "Database")]
public sealed class SampleReviewAccessTests
{
    [DatabaseFact]
    public async Task DrawingWithoutCurationIsRefused()
    {
        await using var world = await SampleReviewWorld.StartAsync();

        var refused = await world.PostAsync(world.DrawRoute, SampleReviewWorld.Annotator);

        Assert.Equal(HttpStatusCode.NotFound, refused.Status);
        Assert.Null((await world.StateAsync())!.Sample);
    }

    [DatabaseFact]
    public async Task MarkingWithoutCurationIsRefused()
    {
        await using var world = await SampleReviewWorld.StartAsync();
        await world.DrawAndReviewAsync(reviewed: 0);

        var refused = await world.PostAsync(world.MarkRoute(1), SampleReviewWorld.Annotator);

        Assert.Equal(HttpStatusCode.NotFound, refused.Status);
        Assert.Equal(0, (await world.StateAsync())!.ReviewedPosition);
    }

    [DatabaseFact]
    public async Task ApprovalWithoutCurationIsRefused()
    {
        await using var world = await SampleReviewWorld.StartAsync();
        await world.DrawAndReviewAsync(reviewed: 2);

        var refused = await world.PostAsync(world.ApproveRoute, SampleReviewWorld.Annotator);

        Assert.Equal(HttpStatusCode.NotFound, refused.Status);
        Assert.Equal(BatchState.Open, (await world.StateAsync())!.State);
    }

    [DatabaseFact]
    public async Task FindingWithoutCurationIsRefused()
    {
        await using var world = await SampleReviewWorld.StartAsync();
        await world.DrawAndReviewAsync(reviewed: 0);

        var screen = await world.GetAsync(world.FindingRoute(1), SampleReviewWorld.Annotator);
        var refused = await world.PostAsync(world.FindingRoute(1), SampleReviewWorld.Annotator, ("achado", "nome"));

        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound), (screen.Status, refused.Status));
        Assert.Equal(BatchState.Open, (await world.StateAsync())!.State);
    }

    [DatabaseFact]
    public async Task ReviewWithoutCurationIsRefusedAndReadsNoText()
    {
        await using var world = await SampleReviewWorld.StartAsync();
        await world.DrawAndReviewAsync(reviewed: 0);

        var refused = await world.GetAsync(world.ReviewRoute, SampleReviewWorld.Annotator);

        Assert.Equal(HttpStatusCode.NotFound, refused.Status);
        Assert.DoesNotContain("ZQXTEXTO", refused.Page, StringComparison.Ordinal);
        Assert.DoesNotContain(await world.StepsAsync("item_text"), step => step.Contains("text_read", StringComparison.Ordinal));
    }

    [DatabaseFact]
    public async Task BatchOfADomainNotGrantedIsNotFoundForEveryStep()
    {
        await using var world = await SampleReviewWorld.StartAsync();

        var screen = await world.GetAsync(world.ReviewRoute, world.Stranger);
        var draw = await world.PostAsync(world.DrawRoute, world.Stranger);
        var approve = await world.PostAsync(world.ApproveRoute, world.Stranger);

        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.NotFound), (screen.Status, draw.Status, approve.Status));
        Assert.Null((await world.StateAsync())!.Sample);
    }

    [DatabaseFact]
    public async Task BatchThatDoesNotExistIsNotFound()
    {
        await using var world = await SampleReviewWorld.StartAsync();

        var screen = await world.GetAsync($"/lotes/{world.Batch + 1000}/revisao", world.Curator);

        Assert.Equal(HttpStatusCode.NotFound, screen.Status);
    }

    [DatabaseFact]
    public async Task ApprovalWithTheDatabaseLostAnswersUnavailable()
    {
        await using var world = await SampleReviewWorld.StartAsync();
        await world.DrawAndReviewAsync(reviewed: 2);
        await world.LoseDatabaseAsync();

        var lost = await world.PostAsync(world.ApproveRoute, world.Curator);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, lost.Status);
    }

    [DatabaseFact]
    public async Task ReloadAfterALostApprovalShowsTheBatchStillOpen()
    {
        await using var world = await SampleReviewWorld.StartAsync();
        await world.DrawAndReviewAsync(reviewed: 2);
        await world.LoseDatabaseAsync();
        _ = await world.PostAsync(world.ApproveRoute, world.Curator);

        await using var fresh = Npgsql.NpgsqlDataSource.Create(world.Submissions.Database.AppConnectionString);
        var held = (await new BatchReviews(fresh, 2).ReadAsync(SubmissionWorld.Domain, world.Batch, TestContext.Current.CancellationToken))!;

        Assert.Equal((BatchState.Open, 2), (held.State, held.ReviewedPosition));
    }
}
