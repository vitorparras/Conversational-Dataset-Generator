using System.Net;
using Core.Host.Screens;
using Core.Host.Tests.Analysis;
using Core.Persistence.Tests.Fixtures;

namespace Core.Host.Tests;

/// <summary>
/// The review of the sample reads the sanitized text of each item (#842, issue 98): only whoever the domain of the batch is granted to reads it, the read is recorded in the chain,
/// every item of the batch already has its sanitized text when the sample is drawn, and what the sanitization removed is shown by the marker of its kind.
/// </summary>
public sealed class SampleReviewSanitizedTests
{
    private const string Read = "sanitized_text_read,content_read";

    [DatabaseFact]
    public async Task SanitizedTextIsReadOnlyWithTheGrantAndTheReadIsRecorded()
    {
        await using var world = await SampleReviewWorld.StartAsync();
        await world.DrawAndReviewAsync(reviewed: 0);

        var refused = await world.GetAsync(world.ReviewRoute, world.Stranger);
        var readsAfterTheRefusal = await ReadsAsync(world);
        var page = await world.GetAsync(world.ReviewRoute, world.Curator);

        Assert.Equal((HttpStatusCode.NotFound, 0), (refused.Status, readsAfterTheRefusal.Count));
        Assert.DoesNotContain("ZQXTEXTO", refused.Page, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal([$"{world.Curator},{Read}", $"{world.Curator},{Read}"], await ReadsAsync(world));
    }

    [DatabaseFact]
    public async Task SampleIsDrawnOnlyAfterEveryItemIsSanitized()
    {
        await using var world = await SampleReviewWorld.StartAsync(messages: 4);
        await world.DrawAndReviewAsync(reviewed: 0);

        var page = await world.GetAsync(world.ReviewRoute, world.Curator);

        Assert.Equal(
            ["4", "0"],
            [.. await world.Submissions.StoredAsync("SELECT count(*)::text FROM content.item_text"),
             .. await world.Submissions.StoredAsync("SELECT count(*)::text FROM content.item_text WHERE sanitized_text IS NULL")]);
        Assert.Equal(2, Occurrences(page.Page, SampleTexts.VersionOf(cleaned: true)));
        Assert.DoesNotContain(SampleTexts.VersionOf(cleaned: false), page.Page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task SampleReviewShowsTheSanitizedTextWithTheTypedMarker()
    {
        await using var world = await SampleReviewWorld.StartAsync(recognizer: AnsweringRecognizer.Finding("CREDENTIAL"));
        await world.DrawAndReviewAsync(reviewed: 0);

        var page = await world.GetAsync(world.ReviewRoute, world.Curator);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(2, Occurrences(page.Page, "[credencial]QXTEXTO"));
        Assert.DoesNotContain("ZQXTEXTO", page.Page, StringComparison.Ordinal);
        Assert.Contains(SampleTexts.VersionOf(cleaned: true), page.Page, StringComparison.Ordinal);
    }

    /// <summary>Who read which text of the items, and why, one line per read, as the owner sees the chain.</summary>
    private static Task<IReadOnlyList<string>> ReadsAsync(SampleReviewWorld world) =>
        world.Submissions.Database.FirstColumnAsOwnerAsync(
            "SELECT coalesce(actor_person_id::text, '-') || ',' || step || ',' || reason FROM public.custody_event WHERE item_kind = 'item_text' ORDER BY item_id");

    private static int Occurrences(string page, string text) => page.Split(text).Length - 1;
}
