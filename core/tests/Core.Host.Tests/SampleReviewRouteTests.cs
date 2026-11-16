using System.Net;
using Core.Host.Screens;
using Core.Host.Tests.Submissions;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;

namespace Core.Host.Tests;

/// <summary>
/// The review of the sample through the screens (issue 822): the draw has the size the installation declares and offers no choice, the items are reviewed in order, a finding rejects
/// the whole batch on a screen of its own, the approval records the sample, who approved and when, and the reading of the texts is in the chain without the texts.
/// </summary>
[Trait("Category", "Database")]
public sealed class SampleReviewRouteTests
{
    [DatabaseFact]
    public async Task SampleHasTheDeclaredSize()
    {
        await using var world = await SampleReviewWorld.StartAsync(size: 2, messages: 4);

        var drawn = await world.PostAsync(world.DrawRoute, world.Curator);
        var page = await world.GetAsync(world.ReviewRoute, world.Curator);

        Assert.Equal(HttpStatusCode.SeeOther, drawn.Status);
        Assert.Equal(2, (await world.StateAsync())!.Sample!.Count);
        Assert.Contains("Item 2 de 2", page.Page, StringComparison.Ordinal);
        Assert.DoesNotContain("Item 3", page.Page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task SampleOffersNoChoiceOfItems()
    {
        await using var world = await SampleReviewWorld.StartAsync(size: 2, messages: 4);

        var before = await world.GetAsync(world.ReviewRoute, world.Curator);
        _ = await world.PostAsync(world.DrawRoute, world.Curator, ("itens", "1,2,3,4"), ("tamanho", "4"));
        var after = await world.GetAsync(world.ReviewRoute, world.Curator);

        Assert.All([before.Page, after.Page], page => Assert.DoesNotContain("type=\"checkbox\"", page, StringComparison.Ordinal));
        Assert.All([before.Page, after.Page], page => Assert.DoesNotContain("name=\"tamanho\"", page, StringComparison.Ordinal));
        Assert.Equal(2, (await world.StateAsync())!.Sample!.Count);
    }

    [DatabaseFact]
    public async Task ItemAfterTheNextCannotBeMarked()
    {
        await using var world = await SampleReviewWorld.StartAsync(size: 2, messages: 4);
        _ = await world.PostAsync(world.DrawRoute, world.Curator);

        var skipped = await world.PostAsync(world.MarkRoute(2), world.Curator);

        Assert.Equal(HttpStatusCode.SeeOther, skipped.Status);
        Assert.Equal(0, (await world.StateAsync())!.ReviewedPosition);
    }

    [DatabaseFact]
    public async Task WithoutADeclaredSampleSizeTheScreenOffersNoDraw()
    {
        await using var world = await SampleReviewWorld.StartAsync(size: null, messages: 4);

        var page = await world.GetAsync(world.ReviewRoute, world.Curator);
        _ = await world.PostAsync(world.DrawRoute, world.Curator);

        Assert.Contains("não declara o tamanho", page.Page, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"sortear\"", page.Page, StringComparison.Ordinal);
        Assert.Null((await world.StateAsync())!.Sample);
    }

    [DatabaseFact]
    public async Task FindingAsksForRejectionOnItsOwnScreen()
    {
        await using var world = await SampleReviewWorld.StartAsync();
        await world.DrawAndReviewAsync(reviewed: 0);

        var page = await world.GetAsync(world.FindingRoute(1), world.Curator);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains("reprovar-lote", page.Page, StringComparison.Ordinal);
        Assert.Equal(BatchState.Open, (await world.StateAsync())!.State);
    }

    [DatabaseFact]
    public async Task RejectionScreenSaysNoItemWillEnter()
    {
        await using var world = await SampleReviewWorld.StartAsync();
        await world.DrawAndReviewAsync(reviewed: 0);

        var page = await world.GetAsync(world.FindingRoute(1), world.Curator);

        Assert.Contains("nenhum item dele entrará no acervo", page.Page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task RejectionRecordsWhoRejected()
    {
        await using var world = await SampleReviewWorld.StartAsync();
        await world.DrawAndReviewAsync(reviewed: 1);

        var rejected = await world.PostAsync(world.FindingRoute(2), world.Curator, ("achado", "nome"));
        var held = (await world.StateAsync())!;

        Assert.Equal(HttpStatusCode.SeeOther, rejected.Status);
        Assert.Equal((BatchState.Rejected, world.Curator), (held.State, held.ReviewerId));
        Assert.Contains($"{world.Curator},batch_reviewed,review_found", await world.StepsAsync("ingestion_batch"));
    }

    [DatabaseFact]
    public async Task RejectionRecordsTheFinding()
    {
        await using var world = await SampleReviewWorld.StartAsync();
        await world.DrawAndReviewAsync(reviewed: 1);

        _ = await world.PostAsync(world.FindingRoute(2), world.Curator, ("achado", "nome"));
        var held = (await world.StateAsync())!;

        Assert.Equal(("nome", 2), (held.Finding, held.FindingPosition));
    }

    [DatabaseFact]
    public async Task RejectionRecordsWhen()
    {
        await using var world = await SampleReviewWorld.StartAsync();
        await world.DrawAndReviewAsync(reviewed: 1);
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);

        _ = await world.PostAsync(world.FindingRoute(2), world.Curator, ("achado", "nome"));
        var held = (await world.StateAsync())!;

        Assert.InRange(held.ReviewedAt!.Value, before, DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [DatabaseFact]
    public async Task FindingOnAnItemAlreadySeenRejectsTheBatch()
    {
        await using var world = await SampleReviewWorld.StartAsync();
        await world.DrawAndReviewAsync(reviewed: 1);

        var rejected = await world.PostAsync(world.FindingRoute(1), world.Curator, ("achado", "endereco"));
        var held = (await world.StateAsync())!;

        Assert.Equal(HttpStatusCode.SeeOther, rejected.Status);
        Assert.Equal((BatchState.Rejected, "endereco", 1), (held.State, held.Finding, held.FindingPosition));
    }

    [DatabaseFact]
    public async Task FindingOnAnItemBeyondTheNextRejectsNothing()
    {
        await using var world = await SampleReviewWorld.StartAsync(size: 3, messages: 6);
        await world.DrawAndReviewAsync(reviewed: 1);

        var refused = await world.PostAsync(world.FindingRoute(3), world.Curator, ("achado", "endereco"));
        var held = (await world.StateAsync())!;

        Assert.EndsWith($"?{SampleReviewScreen.ResultKey}={SampleReviewScreen.RefusedMark}", refused.Location, StringComparison.Ordinal);
        Assert.Equal((BatchState.Open, null), (held.State, held.Finding));
    }

    [DatabaseFact]
    public async Task FindingOfAKindTheScreenDoesNotOfferIsAskedAgainAndRejectsNothing()
    {
        await using var world = await SampleReviewWorld.StartAsync();
        await world.DrawAndReviewAsync(reviewed: 0);

        var refused = await world.PostAsync(world.FindingRoute(1), world.Curator, ("achado", "qualquer-coisa"));

        Assert.Equal(HttpStatusCode.OK, refused.Status);
        Assert.Contains("Escolha o que foi achado.", refused.Page, StringComparison.Ordinal);
        Assert.Equal((BatchState.Open, null), ((await world.StateAsync())!.State, (await world.StateAsync())!.Finding));
    }

    [DatabaseFact]
    public async Task GoingBackFromRejectionLeavesTheBatchOpen()
    {
        await using var world = await SampleReviewWorld.StartAsync();
        await world.DrawAndReviewAsync(reviewed: 0);

        var screen = await world.GetAsync(world.FindingRoute(1), world.Curator);
        var back = await world.GetAsync(world.ReviewRoute, world.Curator);

        Assert.Contains($"href=\"{world.ReviewRoute}\"", screen.Page, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, back.Status);
        Assert.Equal(BatchState.Open, (await world.StateAsync())!.State);
    }

    [DatabaseFact]
    public async Task ApprovalRecordsTheReviewedSample()
    {
        await using var world = await SampleReviewWorld.StartAsync();
        await world.DrawAndReviewAsync(reviewed: 2);
        var drawn = (await world.StateAsync())!.Sample;

        _ = await world.PostAsync(world.ApproveRoute, world.Curator);
        var held = (await world.StateAsync())!;

        Assert.Equal((BatchState.Accepted, 2), (held.State, held.ReviewedPosition));
        Assert.Equal(drawn, held.Sample);
    }

    [DatabaseFact]
    public async Task ApprovalRecordsWhoApproved()
    {
        await using var world = await SampleReviewWorld.StartAsync();
        await world.DrawAndReviewAsync(reviewed: 2);

        var approved = await world.PostAsync(world.ApproveRoute, world.Curator);
        var page = await world.GetAsync(world.ReviewRoute, world.Curator);
        var steps = await world.StepsAsync("ingestion_batch");

        Assert.Equal(HttpStatusCode.SeeOther, approved.Status);
        Assert.Equal(world.Curator, (await world.StateAsync())!.ReviewerId);
        Assert.Contains("Lote aprovado por zqx-curadora", page.Page, StringComparison.Ordinal);
        Assert.Contains($"{world.Curator},batch_reviewed,review_clean", steps);
        Assert.Contains(steps, step => step.Contains("batch_accepted", StringComparison.Ordinal));
    }

    [DatabaseFact]
    public async Task ApprovalRecordsWhen()
    {
        await using var world = await SampleReviewWorld.StartAsync();
        await world.DrawAndReviewAsync(reviewed: 2);
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);

        _ = await world.PostAsync(world.ApproveRoute, world.Curator);
        var held = (await world.StateAsync())!;

        Assert.InRange(held.ReviewedAt!.Value, before, DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [DatabaseFact]
    public async Task NoApprovalWithoutAReviewedSample()
    {
        await using var world = await SampleReviewWorld.StartAsync();
        await world.DrawAndReviewAsync(reviewed: 1);

        var early = await world.PostAsync(world.ApproveRoute, world.Curator);

        Assert.Equal(HttpStatusCode.SeeOther, early.Status);
        Assert.Equal(BatchState.Open, (await world.StateAsync())!.State);
    }

    [DatabaseFact]
    public async Task NoApprovalWithoutADrawnSample()
    {
        await using var world = await SampleReviewWorld.StartAsync();

        _ = await world.PostAsync(world.ApproveRoute, world.Curator);

        Assert.Equal(BatchState.Open, (await world.StateAsync())!.State);
    }

    [DatabaseFact]
    public async Task SampleReadIsRecordedWithoutTheContent()
    {
        await using var world = await SampleReviewWorld.StartAsync();
        await world.DrawAndReviewAsync(reviewed: 0);

        var next = (await world.StateAsync())!.Sample![0];

        var page = await world.GetAsync(world.ReviewRoute, world.Curator);
        var reads = await world.Submissions.Database.FirstColumnAsOwnerAsync(
            "SELECT coalesce(actor_person_id::text, '-') || ',' || item_id || ',' || step || ',' || reason FROM public.custody_event WHERE item_kind = 'item_text' ORDER BY step");

        Assert.Contains("ZQXTEXTO", page.Page, StringComparison.Ordinal);
        Assert.Equal(2, reads.Count);
        Assert.All(reads, read => Assert.StartsWith($"{world.Curator},{next},", read, StringComparison.Ordinal));
        Assert.Equal(["raw_text_read", "sanitized_text_read"], reads.Select(read => read.Split(',')[2]));
        Assert.DoesNotContain(reads, read => read.Contains("ZQXTEXTO", StringComparison.Ordinal));
    }

    [DatabaseFact]
    public async Task DrawGoesBackToTheReviewWithItsMark()
    {
        await using var world = await SampleReviewWorld.StartAsync();

        var drawn = await world.PostAsync(world.DrawRoute, world.Curator);
        var page = await world.GetAsync(drawn.Location!, world.Curator);

        Assert.EndsWith($"{world.ReviewRoute}?{SampleReviewScreen.ResultKey}={SampleReviewScreen.DrawnMark}", drawn.Location, StringComparison.Ordinal);
        Assert.Contains("A amostra foi sorteada.", page.Page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task MarkGoesBackToTheReviewWithItsMark()
    {
        await using var world = await SampleReviewWorld.StartAsync();
        await world.DrawAndReviewAsync(reviewed: 0);

        var marked = await world.PostAsync(world.MarkRoute(1), world.Curator);
        var page = await world.GetAsync(marked.Location!, world.Curator);

        Assert.EndsWith($"?{SampleReviewScreen.ResultKey}={SampleReviewScreen.MarkedMark}", marked.Location, StringComparison.Ordinal);
        Assert.Contains("O item foi marcado como revisado.", page.Page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task StepAlreadyDoneGoesBackWithTheRepeatedMark()
    {
        await using var world = await SampleReviewWorld.StartAsync();
        await world.DrawAndReviewAsync(reviewed: 1);

        var again = await world.PostAsync(world.MarkRoute(1), world.Curator);
        var page = await world.GetAsync(again.Location!, world.Curator);

        Assert.EndsWith($"?{SampleReviewScreen.ResultKey}={SampleReviewScreen.RepeatedMark}", again.Location, StringComparison.Ordinal);
        Assert.Contains("Esse passo já estava feito", page.Page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task StepNotAllowedGoesBackWithTheRefusedMark()
    {
        await using var world = await SampleReviewWorld.StartAsync();
        await world.DrawAndReviewAsync(reviewed: 1);

        var early = await world.PostAsync(world.ApproveRoute, world.Curator);
        var page = await world.GetAsync(early.Location!, world.Curator);

        Assert.EndsWith($"?{SampleReviewScreen.ResultKey}={SampleReviewScreen.RefusedMark}", early.Location, StringComparison.Ordinal);
        Assert.Contains("O passo não foi feito", page.Page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task ApprovalGoesBackWithItsMark()
    {
        await using var world = await SampleReviewWorld.StartAsync();
        await world.DrawAndReviewAsync(reviewed: 2);

        var approved = await world.PostAsync(world.ApproveRoute, world.Curator);

        Assert.EndsWith($"?{SampleReviewScreen.ResultKey}={SampleReviewScreen.ApprovedMark}", approved.Location, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task RejectionGoesBackWithItsMark()
    {
        await using var world = await SampleReviewWorld.StartAsync();
        await world.DrawAndReviewAsync(reviewed: 1);

        var rejected = await world.PostAsync(world.FindingRoute(1), world.Curator, ("achado", "nome"));

        Assert.EndsWith($"?{SampleReviewScreen.ResultKey}={SampleReviewScreen.RejectedMark}", rejected.Location, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task ApprovingACopyOfAnAcceptedFileRejectsItAsAlreadyReceived()
    {
        await using var world = await SampleReviewWorld.StartAsync(size: 2, messages: 4);
        var copy = await world.SubmitCopyAsync();
        await world.Submissions.AcceptReviewedAsync(world.Batch);
        await world.DrawAndReviewAsync(copy, reviewed: 2);

        var approved = await world.PostAsync(SampleReviewWorld.ApproveRouteOf(copy), world.Curator);
        var steps = await world.StepsAsync("ingestion_batch");
        var detail = await world.Submissions.Service.DetailAsync([SubmissionWorld.Domain], copy, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.SeeOther, approved.Status);
        Assert.Equal((BatchState.Rejected, BatchMotives.AlreadyReceived, world.Batch), (detail!.State, detail.Reason, detail.PreviousBatch));
        Assert.Equal(1, steps.Count(step => step.Contains("batch_accepted", StringComparison.Ordinal)));
        Assert.Contains(steps, step => step.StartsWith($"{world.Curator},", StringComparison.Ordinal) && step.Contains("batch_rejected", StringComparison.Ordinal));
    }

    [DatabaseFact]
    public async Task ApprovingACopyLeadsToTheFirstBatch()
    {
        await using var world = await SampleReviewWorld.StartAsync(size: 2, messages: 4);
        var copy = await world.SubmitCopyAsync();
        await world.Submissions.AcceptReviewedAsync(world.Batch);
        await world.DrawAndReviewAsync(copy, reviewed: 2);

        var approved = await world.PostAsync(SampleReviewWorld.ApproveRouteOf(copy), world.Curator);

        Assert.Equal(HttpStatusCode.SeeOther, approved.Status);
        Assert.EndsWith($"/lotes/{world.Batch}", approved.Location, StringComparison.Ordinal);
    }
}
