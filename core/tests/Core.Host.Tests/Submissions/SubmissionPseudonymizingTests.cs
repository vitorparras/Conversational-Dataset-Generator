using System.Globalization;
using System.Text.RegularExpressions;
using Core.Boundary.Contracts;
using Core.Boundary.Fake;
using Core.Host.Submissions;
using Core.Host.Tests.Analysis;
using Core.Ingestion;
using Core.Ingestion.Pseudonymizing;
using Core.Persistence.Content;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.Host.Tests.Submissions;

/// <summary>
/// The submission pseudonymizes every item it keeps (issue 102, F3): the group of each mention is its referent, the messages of one conversation share the marker of a group, a mention
/// the group links to an author gets the marker of the author, and each item is written with its pseudonymized text, its map and its markers, or the batch is rejected with none.
/// </summary>
public sealed partial class SubmissionPseudonymizingTests
{
    private const string Rows = "SELECT count(*) FILTER (WHERE pseudonymized_text IS NOT NULL) || ',' || (SELECT count(*) FROM content.reversal_map) || ',' "
        + "|| (SELECT count(*) FROM content.item_marker) FROM content.item_text";

    private const string MarkersOfTheItems = "SELECT (dense_rank() OVER (ORDER BY item_id) - 1) || ':' || marker || ':' || author FROM content.item_marker ORDER BY item_id, marker";

    private static readonly int[] Granted = [SubmissionWorld.Domain];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [DatabaseFact]
    public async Task PseudonymizedTextHasTheMarkersAndNoSeededOriginal()
    {
        await using var world = await SubmissionWorld.CreateAsync(recognizer: new FakeRecognizer());

        var outcome = await world.SubmitAsync(Conversation(("ZQXANA", "ZQX-FAKE-PERSON-1 viu ZQX-FAKE-PERSON-2"), ("ZQXBETO", "e ZQX-FAKE-PERSON-2 saiu")));

        var texts = await SealedColumns.OpenedAsync(world, ContentColumns.PseudonymizedText, "item_text");
        var (first, second) = (MarkersIn(texts[0])[0], MarkersIn(texts[0])[1]);
        Assert.Equal((SubmissionKind.Done, "2,2,5"), (outcome.Kind, Assert.Single(await world.StoredAsync(Rows))));
        Assert.Equal([$"{first}-1 viu {second}-2", $"e {second}-2 saiu"], texts);
        Assert.NotEqual(first, second);
        Assert.DoesNotContain(texts, text => text.Contains("ZQX", StringComparison.Ordinal));
    }

    [DatabaseFact]
    public async Task BatchRejectedForAnalysisLeavesNoPseudonymizedText()
    {
        await using var world = await SubmissionWorld.CreateAsync(recognizer: AnsweringRecognizer.Unavailable(UnavailabilityReason.ConnectionRefused));

        var outcome = await world.SubmitAsync(SubmissionWorld.Request());
        var detail = await world.Service.DetailAsync(Granted, outcome.Batch!.Value, Token);

        Assert.Equal(
            (BatchState.Rejected, SubmissionStoring.AnalysisUnavailable, "0,0,0", "0"),
            (detail!.State, detail.Reason, Assert.Single(await world.StoredAsync(Rows)), Assert.Single(await world.StoredAsync("SELECT count(*)::text FROM content.marker_in_use"))));
    }

    [DatabaseTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledWhilePseudonymizingLeavesNoItemWithoutItsMap(bool afterTheItems)
    {
        await using var world = await SubmissionWorld.CreateAsync(recognizer: new FakeRecognizer());
        using var cancelling = new CancellationTokenSource();

        var outcome = await CancelledBy(world, cancelling, afterTheItems).SubmitAsync(Conversation(("ZQXANA", "ZQX-FAKE-PERSON-1 viu"), ("ZQXBETO", "ZQX-FAKE-NOTHING de novo")), cancelling.Token);
        var detail = await world.Service.DetailAsync(Granted, outcome.Batch!.Value, Token);

        Assert.Equal(
            (true, BatchState.Rejected, SubmissionStoring.SubmissionCancelled, "0,0,0"),
            (cancelling.IsCancellationRequested, detail!.State, detail.Reason, Assert.Single(await world.StoredAsync(Rows))));
    }

    [DatabaseFact]
    public async Task AuthorGetsAMarkerStoredWithTheItem()
    {
        await using var world = await SubmissionWorld.CreateAsync(recognizer: new FakeRecognizer());

        _ = await world.SubmitAsync(Conversation(("ZQXANA", "ZQX-FAKE-NOTHING"), ("ZQXBETO", "ZQX-FAKE-NOTHING"), ("ZQXANA", "ZQX-FAKE-NOTHING ok")));

        var markers = await world.StoredAsync(MarkersOfTheItems);
        var (ana, beto) = (MarkerOf(markers, 0), MarkerOf(markers, 1));
        var maps = await SealedColumns.OpenedAsync(world, ContentColumns.Entries, "reversal_map");
        Assert.Equal([$"0:{ana}:true", $"1:{beto}:true", $"2:{ana}:true"], markers);
        Assert.NotEqual(ana, beto);
        bool[] held = [maps[0].Contains("ZQXANA", StringComparison.Ordinal), maps[1].Contains("ZQXBETO", StringComparison.Ordinal), maps[2].Contains(ana, StringComparison.Ordinal)];
        Assert.Equal([true, true, true], held);
    }

    [DatabaseFact]
    public async Task MentionLinkedToAnAuthorGetsTheAuthorMarker()
    {
        await using var world = await SubmissionWorld.CreateAsync(recognizer: new FakeRecognizer());

        _ = await world.SubmitAsync(Conversation(("ZQX-FAKE-PERSON-1", "ZQX-FAKE-NOTHING"), ("ZQXBETO", "falei com ZQX-FAKE-PERSON-1")));

        var markers = await world.StoredAsync(MarkersOfTheItems);
        var (author, beto) = (MarkerOf(markers, 0), MarkerOf(markers, 1, but: MarkerOf(markers, 0)));
        Assert.Equal($"falei com {author}-1", (await SealedColumns.OpenedAsync(world, ContentColumns.PseudonymizedText, "item_text"))[1]);
        Assert.Equal([$"0:{author}:true", .. Ordered($"1:{author}:true", $"1:{beto}:true")], markers);
    }

    [DatabaseFact]
    public async Task NicknameOfAnAuthorGetsItsOwnMarker()
    {
        await using var world = await SubmissionWorld.CreateAsync(recognizer: new FakeRecognizer());

        _ = await world.SubmitAsync(Conversation(("ZQX-FAKE-PERSON-1", "ZQX-FAKE-NOTHING"), ("ZQXBETO", "falei com ZQX-FAKE-PERSON-2")));

        var texts = await SealedColumns.OpenedAsync(world, ContentColumns.PseudonymizedText, "item_text");
        var markers = await world.StoredAsync(MarkersOfTheItems);
        var (author, nickname) = (MarkerOf(markers, 0), MarkersIn(texts[1])[0]);
        Assert.Equal($"falei com {nickname}-2", texts[1]);
        Assert.NotEqual(author, nickname);
        Assert.Contains($"1:{nickname}:false", markers);
    }

    [DatabaseFact]
    public async Task HomonymsInOneConversationShareTheMarker()
    {
        await using var world = await SubmissionWorld.CreateAsync(recognizer: new FakeRecognizer());

        _ = await world.SubmitAsync(Conversation(("ZQXANA", "ZQX-FAKE-PERSON-3 chegou"), ("ZQXBETO", "outro ZQX-FAKE-PERSON-3 saiu")));

        var texts = await SealedColumns.OpenedAsync(world, ContentColumns.PseudonymizedText, "item_text");
        var homonym = MarkersIn(texts[0])[0];
        Assert.Equal([$"{homonym}-3 chegou", $"outro {homonym}-3 saiu"], texts);
    }

    private static SubmissionRequest Conversation(params (string Sender, string Text)[] messages) => SubmissionWorld.Request(SubmissionWorld.Messages(messages));

    /// <summary>The markers written in a text, in the order they appear.</summary>
    private static string[] MarkersIn(string text) => [.. Marker().Matches(text).Select(found => found.Value)];

    /// <summary>The marker of the item at the position given that the rows hold, the only one there but the one excluded.</summary>
    private static string MarkerOf(IEnumerable<string> rows, int item, string? but = null) =>
        Assert.Single(rows.Select(row => row.Split(':')), parts => parts[0] == item.ToString(CultureInfo.InvariantCulture) && parts[1] != but)[1];

    private static string[] Ordered(params string[] rows) => [.. rows.Order(StringComparer.Ordinal)];

    /// <summary>A service over the world that cancels the submission the first time a text is measured: by the swap, before any item, or by the marking, after all of them.</summary>
    private static SubmissionService CancelledBy(SubmissionWorld world, CancellationTokenSource cancelling, bool afterTheItems)
    {
        var conditions = OriginConditions.Of([SubmissionWorld.Strictest, "zqx_media", SubmissionWorld.Loosest]);
        var rule = new ReplicationRule(minLength: 10, minSenders: 2);
        var intake = afterTheItems
            ? SubmissionWorld.Intake(1_000_000, conditions, world.Content, world.DataSource, rule, new CancellingBoundaries(cancelling), new FakeRecognizer())
            : SubmissionWorld.Intake(1_000_000, conditions, world.Content, world.DataSource, recognizer: new FakeRecognizer()) with
            {
                Swap = new PseudonymSwap(new CancellingBoundaries(cancelling), (elsewhere, known) => new ConversationMarkers(new MarkerDraw(), elsewhere, known)),
            };
        return new SubmissionService(intake, world.Batches, world.DataSource, NullLogger.Instance);
    }

    [GeneratedRegex("(PESSOA|LUGAR|ORGANIZACAO)_[0-9]{8}")]
    private static partial Regex Marker();
}
