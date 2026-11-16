using System.Net;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;

namespace Core.Host.Tests;

/// <summary>
/// The review of the sample when two requests reach the same batch together (issue 822), each over a transaction of its own: the one that reaches the chain second is not an error, it
/// sees what the first wrote, and the batch ends with one draw, one review and one mark. The log of the host carries nothing of the text that was read.
/// </summary>
[Trait("Category", "Database")]
public sealed class SampleReviewDisputeTests
{
    private static readonly string[] Findings = ["endereco", "outro"];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [DatabaseFact]
    public async Task DrawingTwiceShowsTheSampleAlreadyDrawn()
    {
        await using var world = await SampleReviewWorld.StartAsync(size: 2, messages: 4);

        var replies = await TogetherAsync(() => world.PostAsync(world.DrawRoute, world.Curator), () => world.PostAsync(world.DrawRoute, world.Curator));
        var page = await world.GetAsync(world.ReviewRoute, world.Curator);

        Assert.All(replies, reply => Assert.Equal(HttpStatusCode.SeeOther, reply.Status));
        Assert.Equal(2, (await world.StateAsync())!.Sample!.Count);
        Assert.Equal(1, (await world.StepsAsync("ingestion_batch")).Count(step => step.Contains("batch_sampled", StringComparison.Ordinal)));
        Assert.Contains("Item 2 de 2", page.Page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task ReviewLostToAnotherShowsWhoReviewedFirst()
    {
        await using var world = await SampleReviewWorld.StartAsync(size: 2, messages: 4);
        await world.DrawAndReviewAsync(reviewed: 1);

        var replies = await TogetherAsync(
            () => world.PostAsync(world.FindingRoute(2), world.Curator, ("achado", "endereco")),
            () => world.PostAsync(world.FindingRoute(2), world.Curator, ("achado", "outro")));
        var held = (await world.StateAsync())!;
        var page = await world.GetAsync(world.ReviewRoute, world.Curator);

        Assert.All(replies, reply => Assert.Equal(HttpStatusCode.SeeOther, reply.Status));
        Assert.Equal(BatchState.Rejected, held.State);
        Assert.Contains(held.Finding!, Findings);
        Assert.Equal(1, (await world.StepsAsync("ingestion_batch")).Count(step => step.Contains("batch_reviewed", StringComparison.Ordinal)));
        Assert.Contains("Lote reprovado por zqx-curadora", page.Page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task MarkingTheSameItemTwiceKeepsOneMark()
    {
        await using var world = await SampleReviewWorld.StartAsync(size: 2, messages: 4);
        await world.DrawAndReviewAsync(reviewed: 0);

        var replies = await TogetherAsync(() => world.PostAsync(world.MarkRoute(1), world.Curator), () => world.PostAsync(world.MarkRoute(1), world.Curator));
        var once = (await world.StateAsync())!.ReviewedPosition;
        var next = await world.PostAsync(world.MarkRoute(2), world.Curator);

        Assert.All(replies, reply => Assert.Equal(HttpStatusCode.SeeOther, reply.Status));
        Assert.Equal(1, once);
        Assert.Equal(HttpStatusCode.SeeOther, next.Status);
        Assert.Equal(2, (await world.StateAsync())!.ReviewedPosition);
    }

    [DatabaseFact]
    public async Task SampleReadWritesNoTextToTheLog()
    {
        using var log = new RecordedLog();
        await using var world = await SampleReviewWorld.StartAsync(size: 2, messages: 4, log);
        await world.DrawAndReviewAsync(reviewed: 2);

        var read = await world.GetAsync(world.ReviewRoute, world.Curator);
        _ = await world.PostAsync(world.ApproveRoute, world.Curator);
        var closed = await world.GetAsync(world.ReviewRoute, world.Curator);

        Assert.Contains("ZQXTEXTO", read.Page, StringComparison.Ordinal);
        Assert.Equal(BatchState.Accepted, (await world.StateAsync())!.State);
        Assert.DoesNotContain(log.Lines, line => line.Contains("ZQXTEXTO", StringComparison.Ordinal));
        Assert.DoesNotContain("ZQXTEXTO", closed.Page, StringComparison.Ordinal);
    }

    private static Task<Reply[]> TogetherAsync(params Func<Task<Reply>>[] requests) => Task.WhenAll(requests.Select(request => Task.Run(request, Token)));
}
