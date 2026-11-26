using System.Net;
using System.Text.RegularExpressions;
using Core.Boundary.Fake;
using Core.Boundary.Text;
using Core.Host.Endpoints;
using Core.Host.Tests.Submissions;
using Core.Ingestion.Sanitizing;
using Core.Persistence.Content;
using Core.Persistence.Tests.Fixtures;

namespace Core.Host.Tests;

/// <summary>
/// The rerun of a pseudonymized item (issue 102, F3): the item whose sanitized text changes is pseudonymized again in the same transaction, with the markers of its own map matched by the
/// original, its old map emptied and a new one written; an item whose text is the same keeps its pseudonymized text and its map. Items stored before the coreference get their first
/// pseudonymized text through the queue, by the groups the service gives, and never share a marker; the screen offers them even when in the version in force.
/// </summary>
public sealed class RerunPseudonymizingTests
{
    private const string Maps = "SELECT count(*) FILTER (WHERE emptied_at IS NULL) || ',' || count(*) FROM content.reversal_map";

    private const string MarkersOfTheItems = "SELECT item_id || ':' || marker || ':' || author FROM content.item_marker ORDER BY item_id, marker";

    /// <summary>Takes from the items of the world what the coreference gave them, as an item stored before it holds: no pseudonymized text, no map and no marker.</summary>
    private static readonly string BeforeTheCoreference = Before(null);

    /// <summary>As <see cref="BeforeTheCoreference"/>, for the first item of the world alone.</summary>
    private static readonly string FirstBeforeTheCoreference = Before("(SELECT min(id) FROM content.item_text)");

    [DatabaseFact]
    public async Task ResanitizedItemIsPseudonymizedAgainInTheSameTransaction()
    {
        var request = SubmissionWorld.Request(SubmissionWorld.Messages(("ZQXANA", "ZQX-FAKE-PERSON-1 liga (00) 90000-0000 ok"), ("ZQXBETO", "e ZQX-FAKE-PERSON-1 saiu")));
        await using var world = await RerunWorld.CreateAsync(new FakeRecognizer(), request);
        var before = await SealedColumns.OpenedAsync(world.Submissions, ContentColumns.PseudonymizedText, "item_text");
        var markers = await world.Submissions.StoredAsync(MarkersOfTheItems);
        var rerun = await world.RequestAsync(CompositionRoot.StepsOf());
        await world.StartAsync(new SanitizationRerun(new Sanitizer(CompositionRoot.StepsOf(), new TextBoundaries())));

        await world.IdleAsync();

        var progress = await world.ProgressAsync(rerun);
        var person = before[0][..15];
        var maps = await SealedColumns.OpenedAsync(world.Submissions, ContentColumns.Entries, "reversal_map", "WHERE emptied_at IS NULL");
        Assert.Equal((1L, 1L, 0L), (progress.Replaced, progress.Identical, progress.Refused));
        Assert.Equal([$"{person}-1 liga (00) 90000-0000 ok", $"e {person}-1 saiu"], before);
        Assert.Equal([$"{person}-1 liga [contato] ok", $"e {person}-1 saiu"], await SealedColumns.OpenedAsync(world.Submissions, ContentColumns.PseudonymizedText, "item_text"));
        Assert.Equal("2,3", Assert.Single(await world.Submissions.StoredAsync(Maps)));
        Assert.Equal(markers, await world.Submissions.StoredAsync(MarkersOfTheItems));
        Assert.Equal([true, true], [maps[0].Contains(person, StringComparison.Ordinal), maps[0].Contains("ZQX-FAKE-PERSON", StringComparison.Ordinal)]);
    }

    [DatabaseFact]
    public async Task OldItemsGetTheirFirstPseudonymizedThroughTheQueue()
    {
        var request = SubmissionWorld.Request(SubmissionWorld.Messages(("ZQXANA", "ZQX-FAKE-PERSON-1 liga (00) 90000-0000 ok"), ("ZQXBETO", "e ZQX-FAKE-PERSON-1 saiu")));
        await using var world = await RerunWorld.CreateAsync(new FakeRecognizer(), request);
        await world.Submissions.Database.ExecuteAsSuperuserAsync(BeforeTheCoreference);
        var rerun = await world.RequestAsync(CompositionRoot.StepsOf());
        await world.StartAsync(new SanitizationRerun(new Sanitizer(CompositionRoot.StepsOf(), new TextBoundaries())));

        await world.IdleAsync();

        var progress = await world.ProgressAsync(rerun);
        var after = await SealedColumns.OpenedAsync(world.Submissions, ContentColumns.PseudonymizedText, "item_text");
        var (first, second) = (after[0][..15], after[1][2..17]);
        Assert.Equal((1L, 1L, 0L), (progress.Replaced, progress.Identical, progress.Refused));
        Assert.Equal([$"{first}-1 liga [contato] ok", $"e {second}-1 saiu"], after);
        Assert.Equal((true, true), (first.StartsWith("PESSOA_", StringComparison.Ordinal), first != second));
        Assert.Equal("2,2", Assert.Single(await world.Submissions.StoredAsync(Maps)));
    }

    // Every item in the version in force, one of them stored before the coreference: the screen offers the one the queue takes, and the confirmation queues it.
    [DatabaseFact]
    public async Task OldItemInTheVersionInForceIsOfferedAndQueued()
    {
        await using var world = await RerunScreensWorld.StartAsync();
        _ = await world.Rerun.RequestAsync(CompositionRoot.StepsOf());
        await world.ConcludeOneAsync();
        await world.ConcludeOneAsync();
        await world.Rerun.Submissions.Database.ExecuteAsSuperuserAsync(FirstBeforeTheCoreference);

        var page = await world.GetAsync(ScreenPaths.Rerun);
        var confirm = await world.GetAsync(ScreenPaths.RerunConfirm);
        var queued = await world.ConfirmAsync();

        Assert.DoesNotContain("Nada a reexecutar.", page.Page, StringComparison.Ordinal);
        Assert.Matches(new Regex($"href=\"{ScreenPaths.RerunConfirm}\"[^>]*>Reexecutar 1 item</a>", RegexOptions.None, TimeSpan.FromSeconds(1)), page.Page);
        Assert.Contains("Pôr 1 item na fila", confirm.Page, StringComparison.Ordinal);
        var open = Assert.NotNull((await world.SituationAsync()).OpenRerunId);
        Assert.Equal((HttpStatusCode.SeeOther, ScreenPaths.RerunProgressOf(open), 1L), (queued.Status, queued.Location, (await world.Rerun.ProgressAsync(open)).Queued));
    }

    /// <summary>Takes from every item, or from the one the query names, what the coreference gave it, with the triggers of the content off while it does.</summary>
    private static string Before(string? item)
    {
        var (ofItem, ofText) = item is null ? ("", "") : ($" WHERE item_id = {item}", $" WHERE id = {item}");
        return "ALTER TABLE content.item_text DISABLE TRIGGER USER; ALTER TABLE content.reversal_map DISABLE TRIGGER USER; ALTER TABLE content.item_marker DISABLE TRIGGER USER; "
            + $"DELETE FROM content.item_marker{ofItem}; DELETE FROM content.reversal_map{ofItem}; UPDATE content.item_text SET pseudonymized_text = NULL{ofText}; "
            + "ALTER TABLE content.item_text ENABLE TRIGGER USER; ALTER TABLE content.reversal_map ENABLE TRIGGER USER; ALTER TABLE content.item_marker ENABLE TRIGGER USER";
    }
}
