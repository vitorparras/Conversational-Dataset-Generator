using Core.Boundary.Fake;
using Core.Boundary.Text;
using Core.Host.Tests.Submissions;
using Core.Ingestion.Sanitizing;
using Core.Persistence.Content;
using Core.Persistence.Tests.Fixtures;

namespace Core.Host.Tests;

/// <summary>
/// The rerun of a pseudonymized item (issue 102, F3): the item whose sanitized text changes is pseudonymized again in the same transaction, with the markers of its own map matched by the
/// original, its old map emptied and a new one written; an item whose text is the same keeps its pseudonymized text and its map.
/// </summary>
public sealed class RerunPseudonymizingTests
{
    private const string Maps = "SELECT count(*) FILTER (WHERE emptied_at IS NULL) || ',' || count(*) FROM content.reversal_map";

    private const string MarkersOfTheItems = "SELECT item_id || ':' || marker || ':' || author FROM content.item_marker ORDER BY item_id, marker";

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
}
