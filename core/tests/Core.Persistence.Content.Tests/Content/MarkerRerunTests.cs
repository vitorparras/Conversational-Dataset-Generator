using Core.Persistence.Content.Rerun;
using Core.Persistence.Content.Writing;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// The markers of an item through the rerun of its sanitization (issue 101): the ones it holds are kept as they are, with no new draw, a new one is held and given to it, and one gone from its
/// text leaves the item and stays held in the installation, so that it never comes back in another conversation; a marker another item holds is never given to the item rerun.
/// </summary>
[Trait("Category", "Database")]
public sealed class MarkerRerunTests : IClassFixture<RerunDatabase>
{
    private const string NewText = "texto sanitizado de novo zqx";

    private readonly RerunDatabase database;

    public MarkerRerunTests(RerunDatabase database)
    {
        this.database = database;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [DatabaseFact]
    public async Task ResanitizedItemKeepsItsMarkersWithoutDrawingAgain()
    {
        var rerun = await database.CaseAsync();
        var markers = PseudonymizedTexts.Draw(2);
        var (kept, added) = (markers[0], markers[1]);
        var item = Assert.NotNull((await rerun.PseudonymizedAsync(PseudonymizedTexts.Holding(kept))).ItemId);
        _ = await rerun.RequestAsync();

        // The rerun says the marker it kept stands for an author: the row the item holds is not written again, so it stays as the first writing left it.
        var result = await rerun.Resanitizer.ResanitizeAsync(await rerun.TakenAsync(), Rerunning([new HeldMarker(kept, true), new HeldMarker(added, false)]), Token);

        Assert.Equal(RerunOutcome.Replaced, result.Outcome);
        Assert.Equal($"{Joined($"{kept}:false", $"{added}:false")}|{Joined(kept, added)}", await rerun.MarkersOfAsync(item));
    }

    [DatabaseFact]
    public async Task MarkerGoneFromTheTextStaysReserved()
    {
        var rerun = await database.CaseAsync();
        var markers = PseudonymizedTexts.Draw(2);
        var (stays, gone) = (markers[0], markers[1]);
        var item = Assert.NotNull((await rerun.PseudonymizedAsync(PseudonymizedTexts.Holding(stays, gone))).ItemId);
        _ = await rerun.RequestAsync();

        var result = await rerun.Resanitizer.ResanitizeAsync(await rerun.TakenAsync(), Rerunning([new HeldMarker(stays, false)]), Token);
        var another = await rerun.PseudonymizedAsync(PseudonymizedTexts.Holding(gone));

        Assert.Equal(RerunOutcome.Replaced, result.Outcome);
        Assert.Equal($"{stays}:false|{Joined(stays, gone)}", await rerun.MarkersOfAsync(item));
        Assert.Equal((null, gone), (another.ItemId, Assert.Single(another.Taken)));
    }

    [DatabaseFact]
    public async Task RerunNeverGivesAnItemAMarkerAnotherItemHolds()
    {
        var rerun = await database.CaseAsync();
        var markers = PseudonymizedTexts.Draw(2);
        var (x, y) = (markers[0], markers[1]);
        var a = Assert.NotNull((await rerun.PseudonymizedAsync(PseudonymizedTexts.Holding(x))).ItemId);
        var b = Assert.NotNull((await rerun.PseudonymizedAsync(PseudonymizedTexts.Holding(y))).ItemId);
        _ = await rerun.RequestAsync();
        var held = await rerun.TakenAsync();

        var refused = await Assert.ThrowsAsync<PostgresException>(() => rerun.Resanitizer.ResanitizeAsync(held, Rerunning([new HeldMarker(x, false), new HeldMarker(y, false)]), Token));

        Assert.Equal(("CE005", "a marker new to an item is one won in the same transaction"), (refused.SqlState, refused.MessageText));
        Assert.Equal(($"{x}:false|{Joined(x, y)}", $"{y}:false|{Joined(x, y)}"), (await rerun.MarkersOfAsync(a), await rerun.MarkersOfAsync(b)));
    }

    private static Func<RerunSource, Resanitized> Rerunning(IReadOnlyList<HeldMarker> markers) => source => RerunCase.Rerunning(NewText)(source) with { Markers = markers };

    private static string Joined(params string[] values) => string.Join(',', values.Order(StringComparer.Ordinal));
}
