using System.Net;
using Core.Host.Screens;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;

namespace Core.Host.Tests;

/// <summary>
/// The review of the sample when two people reach the same batch together (issue 822), each over a transaction of its own: the draw, the review and the approval wait for the turn of the
/// batch, and the one that reaches the chain second is not an error, it sees what the first wrote. The mark is settled by one atomic update. The log of the host carries nothing of the text.
/// </summary>
[Trait("Category", "Database")]
public sealed class SampleReviewDisputeTests
{
    private static readonly string[] Findings = ["endereco", "outro"];

    [DatabaseFact]
    public async Task DrawingTwiceShowsTheSampleAlreadyDrawn()
    {
        await using var world = await SampleReviewWorld.StartAsync(size: 2, messages: 4);

        var replies = await world.TogetherAsync(() => world.PostAsync(world.DrawRoute, world.Curator), () => world.PostAsync(world.DrawRoute, world.Colleague));
        var page = await world.GetAsync(world.ReviewRoute, world.Curator);

        Assert.All(replies, reply => Assert.Equal(HttpStatusCode.SeeOther, reply.Status));
        Assert.Equal(Sorted(SampleReviewScreen.DrawnMark, SampleReviewScreen.RepeatedMark), MarksOf(replies));
        Assert.Equal(2, (await world.StateAsync())!.Sample!.Count);
        Assert.Equal(1, (await world.StepsAsync("ingestion_batch")).Count(step => step.Contains("batch_sampled", StringComparison.Ordinal)));
        Assert.Contains("Item 2 de 2", page.Page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task ReviewLostToAnotherShowsWhoReviewedFirst()
    {
        await using var world = await SampleReviewWorld.StartAsync(size: 2, messages: 4);
        await world.DrawAndReviewAsync(reviewed: 1);

        var replies = await world.TogetherAsync(
            () => world.PostAsync(world.FindingRoute(2), world.Curator, ("achado", "endereco")),
            () => world.PostAsync(world.FindingRoute(2), world.Colleague, ("achado", "outro")));
        var held = (await world.StateAsync())!;
        var page = await world.GetAsync(world.ReviewRoute, world.Curator);

        Assert.All(replies, reply => Assert.Equal(HttpStatusCode.SeeOther, reply.Status));
        Assert.Equal(Sorted(SampleReviewScreen.RejectedMark, SampleReviewScreen.RepeatedMark), MarksOf(replies));
        Assert.Equal(BatchState.Rejected, held.State);
        Assert.Contains(held.Finding!, Findings);
        Assert.Equal(1, (await world.StepsAsync("ingestion_batch")).Count(step => step.Contains("batch_reviewed", StringComparison.Ordinal)));
        Assert.Contains($"Lote reprovado por {(held.ReviewerId == world.Curator ? "zqx-curadora" : "zqx-colega")}", page.Page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task MarkingTheSameItemTwiceKeepsOneMark()
    {
        await using var world = await SampleReviewWorld.StartAsync(size: 2, messages: 4);
        await world.DrawAndReviewAsync(reviewed: 0);

        var replies = await Task.WhenAll(world.PostAsync(world.MarkRoute(1), world.Curator), world.PostAsync(world.MarkRoute(1), world.Colleague));
        var once = (await world.StateAsync())!.ReviewedPosition;
        var next = await world.PostAsync(world.MarkRoute(2), world.Curator);

        Assert.All(replies, reply => Assert.Equal(HttpStatusCode.SeeOther, reply.Status));
        Assert.Equal(Sorted(SampleReviewScreen.MarkedMark, SampleReviewScreen.RepeatedMark), MarksOf(replies));
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
        Assert.Contains(log.Lines, line => line.Contains(world.ReviewRoute, StringComparison.Ordinal));
        Assert.DoesNotContain(log.Lines, line => line.Contains("ZQXTEXTO", StringComparison.Ordinal));
        Assert.DoesNotContain("ZQXTEXTO", closed.Page, StringComparison.Ordinal);
    }

    /// <summary>The marks the replies carry in their addresses, in alphabetical order, so that which of the two came first does not matter.</summary>
    private static string[] MarksOf(IEnumerable<Reply> replies) =>
        [.. replies.Select(reply => reply.Location!).Select(location => location[(location.IndexOf('=', StringComparison.Ordinal) + 1)..]).Order(StringComparer.Ordinal)];

    private static string[] Sorted(params string[] marks) => [.. marks.Order(StringComparer.Ordinal)];
}
