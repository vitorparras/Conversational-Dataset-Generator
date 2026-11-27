using System.Net;
using Core.Boundary.Fake;
using Core.Host.Screens;
using Core.Host.Tests.Submissions;
using Core.Persistence.Content;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;

namespace Core.Host.Tests;

/// <summary>
/// The review of the sample shows only the pseudonymized text (issue 939): no original seeded in the conversation reaches the page of the review nor the one of the finding (CA-1); an item
/// without the pseudonymized text shows no text, the screen says why, nothing is read and the mark is refused (CA-2); a sample with such an item is not approved even with every place
/// marked, and a finding still rejects it (CA-3); and each text shown has its reading in the chain, of the pseudonymized text only (CA-4).
/// </summary>
public sealed class SampleReviewPseudonymizedTests
{
    private const string Mention = "ZQX-FAKE-PERSON-1";

    private const string Strip = "ALTER TABLE content.item_text DISABLE TRIGGER USER; UPDATE content.item_text SET pseudonymized_text = NULL; ALTER TABLE content.item_text ENABLE TRIGGER USER";

    [DatabaseFact]
    public async Task Ca1TheReviewShowsThePseudonymizedTextAndNoOriginalSeededReachesTheReviewNorTheFinding()
    {
        await using var world = await SampleReviewWorld.StartAsync(recognizer: new FakeRecognizer(), mention: Mention);
        await world.DrawAndReviewAsync(reviewed: 0);

        var review = await world.GetAsync(world.ReviewRoute, world.Curator);
        var finding = await world.GetAsync(world.FindingRoute(1), world.Curator);

        var pseudonymized = await SealedColumns.OpenedAsync(world.Submissions, ContentColumns.PseudonymizedText, "item_text");
        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (review.Status, finding.Status));
        Assert.Equal(2, pseudonymized.Count(text => review.Page.Contains(text, StringComparison.Ordinal)));
        Assert.DoesNotContain(pseudonymized, text => text.Contains(Mention, StringComparison.Ordinal));
        Assert.DoesNotContain("ZQX-FAKE", review.Page, StringComparison.Ordinal);
        Assert.DoesNotContain("ZQX-FAKE", finding.Page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task Ca2AnItemWithoutThePseudonymizedTextShowsNoTextSaysWhyReadsNothingAndIsNotMarked()
    {
        await using var world = await SampleReviewWorld.StartAsync();
        await world.DrawAndReviewAsync(reviewed: 0);
        await world.Submissions.Database.ExecuteAsSuperuserAsync(Strip);

        var review = await world.GetAsync(world.ReviewRoute, world.Curator);
        var mark = await world.PostAsync(world.MarkRoute(1), world.Curator);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.SeeOther), (review.Status, mark.Status));
        Assert.Equal(2, review.Page.Split(SampleTexts.Absent).Length - 1);
        Assert.DoesNotContain("ZQXTEXTO", review.Page, StringComparison.Ordinal);
        Assert.DoesNotContain(world.MarkRoute(1), review.Page, StringComparison.Ordinal);
        Assert.Contains(world.FindingRoute(1), review.Page, StringComparison.Ordinal);
        Assert.DoesNotContain(await world.StepsAsync("item_text"), step => step.Contains("text_read", StringComparison.Ordinal));
        Assert.Equal(0, (await world.StateAsync())!.ReviewedPosition);
    }

    [DatabaseFact]
    public async Task Ca3ASampleWithAnItemWithoutThePseudonymizedTextIsNotApprovedEvenWithEveryPlaceMarked()
    {
        await using var world = await SampleReviewWorld.StartAsync();
        await world.DrawAndReviewAsync(reviewed: 2);
        await world.Submissions.Database.ExecuteAsSuperuserAsync(Strip);

        var approved = await world.PostAsync(world.ApproveRoute, world.Curator);

        var held = (await world.StateAsync())!;
        Assert.Equal(HttpStatusCode.SeeOther, approved.Status);
        Assert.Equal((BatchState.Open, null, 2), (held.State, held.ReviewerId, held.ReviewedPosition));
        Assert.DoesNotContain(await world.StepsAsync("ingestion_batch"), step => step.Contains("batch_reviewed", StringComparison.Ordinal));
    }

    [DatabaseFact]
    public async Task Ca3AFindingStillRejectsASampleWithAnItemWithoutThePseudonymizedText()
    {
        await using var world = await SampleReviewWorld.StartAsync();
        await world.DrawAndReviewAsync(reviewed: 0);
        await world.Submissions.Database.ExecuteAsSuperuserAsync(Strip);

        var rejected = await world.PostAsync(world.FindingRoute(1), world.Curator, ("achado", "nome"));

        Assert.Equal(HttpStatusCode.SeeOther, rejected.Status);
        Assert.Equal(BatchState.Rejected, (await world.StateAsync())!.State);
    }

    [DatabaseFact]
    public async Task Ca4EachTextShownHasItsReadingOfThePseudonymizedTextInTheChain()
    {
        await using var world = await SampleReviewWorld.StartAsync();
        await world.DrawAndReviewAsync(reviewed: 0);

        var review = await world.GetAsync(world.ReviewRoute, world.Curator);

        Assert.Equal(HttpStatusCode.OK, review.Status);
        Assert.Equal([$"{world.Curator},pseudonymized_text_read,content_read", $"{world.Curator},pseudonymized_text_read,content_read"], await world.StepsAsync("item_text"));
        Assert.Empty(await world.Submissions.Database.FirstColumnAsOwnerAsync("SELECT step FROM public.custody_event WHERE step = 'reversal_entries_read'"));
    }
}
