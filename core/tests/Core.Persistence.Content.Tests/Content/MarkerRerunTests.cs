using Core.Persistence.Content;
using Core.Persistence.Content.Rerun;
using Core.Persistence.Content.Writing;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// The markers of an item through the rerun of its sanitization (issues 101 and 102): the rerun is given the map the item holds, the ones it holds are kept as they are, with no new draw,
/// a new one is won for its batch and given to it, and one gone from its text leaves the item and stays held in the installation, so that it never comes back in another conversation; a
/// marker an item of another batch holds is never given to the item rerun, which is drawn again without it.
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
        var seen = new List<RerunSource>();

        // The rerun says the marker it kept stands for an author: the row the item holds is not written again, so it stays as the first writing left it.
        var result = await rerun.Resanitizer.ResanitizeAsync(await rerun.TakenAsync(), Remapping(_ => [Entry(kept, true), Entry(added)], seen.Add), Token);

        Assert.Equal(RerunOutcome.Replaced, result.Outcome);
        Assert.Equal($"{Joined($"{kept}:false", $"{added}:false")}|{Joined(kept, added)}", await rerun.MarkersOfAsync(item));
        var source = Assert.Single(seen);
        Assert.Equal((item, $"{kept}:{PseudonymizedTexts.OriginalOf(kept)}:False"), (source.ItemId, string.Join(';', source.Map!.Select(entry => $"{entry.Marker}:{entry.Original}:{entry.Author}"))));
    }

    [DatabaseFact]
    public async Task NewMarkerOfTwoOriginalsStandsForAnAuthorWhenOneOfThemIs()
    {
        var rerun = await database.CaseAsync();
        var markers = PseudonymizedTexts.Draw(2);
        var (kept, added) = (markers[0], markers[1]);
        var item = Assert.NotNull((await rerun.PseudonymizedAsync(PseudonymizedTexts.Holding(kept))).ItemId);
        _ = await rerun.RequestAsync();

        // Only the second original of the new marker is the author: the marker stands for an author all the same.
        var result = await rerun.Resanitizer.ResanitizeAsync(
            await rerun.TakenAsync(), Remapping(_ => [Entry(kept), Entry(added), new(added, "ZQXAUTORA", true, OccurrenceDestination.Pseudonymize)]), Token);

        Assert.Equal(RerunOutcome.Replaced, result.Outcome);
        Assert.Equal($"{Joined($"{kept}:false", $"{added}:true")}|{Joined(kept, added)}", await rerun.MarkersOfAsync(item));
    }

    [DatabaseFact]
    public async Task MarkerGoneFromTheTextStaysReserved()
    {
        var rerun = await database.CaseAsync();
        var markers = PseudonymizedTexts.Draw(2);
        var (stays, gone) = (markers[0], markers[1]);
        var item = Assert.NotNull((await rerun.PseudonymizedAsync(PseudonymizedTexts.Holding(stays, gone))).ItemId);
        _ = await rerun.RequestAsync();

        var result = await rerun.Resanitizer.ResanitizeAsync(await rerun.TakenAsync(), Remapping(_ => [Entry(stays)]), Token);
        var another = await rerun.PseudonymizedAsync(PseudonymizedTexts.Holding(gone), await rerun.AnotherBatchAsync());

        Assert.Equal(RerunOutcome.Replaced, result.Outcome);
        Assert.Equal($"{stays}:false|{Joined(stays, gone)}", await rerun.MarkersOfAsync(item));
        Assert.Equal((null, gone), (another.ItemId, Assert.Single(another.Taken)));
    }

    [DatabaseFact]
    public async Task RerunDrawsAgainWithoutAMarkerAnotherBatchHolds()
    {
        var rerun = await database.CaseAsync();
        var markers = PseudonymizedTexts.Draw(3);
        var (x, y, z) = (markers[0], markers[1], markers[2]);
        var a = Assert.NotNull((await rerun.PseudonymizedAsync(PseudonymizedTexts.Holding(x))).ItemId);
        _ = Assert.NotNull((await rerun.PseudonymizedAsync(PseudonymizedTexts.Holding(y), await rerun.AnotherBatchAsync())).ItemId);
        _ = await rerun.RequestAsync();
        var seen = new List<RerunSource>();

        var result = await rerun.Resanitizer.ResanitizeAsync(await rerun.TakenAsync(), Remapping(source => source.Taken.Contains(y) ? [Entry(x), Entry(z)] : [Entry(x), Entry(y)], seen.Add), Token);

        Assert.Equal(RerunOutcome.Replaced, result.Outcome);
        Assert.Equal(["", y], seen.Select(source => string.Join(',', source.Taken)));
        Assert.Equal($"{Joined($"{x}:false", $"{z}:false")}|{Joined(x, y, z)}", await rerun.MarkersOfAsync(a));
    }

    [DatabaseFact]
    public async Task RerunNeverGivesAnItemAMarkerAnotherItemHolds()
    {
        var rerun = await database.CaseAsync();
        var markers = PseudonymizedTexts.Draw(2);
        var (x, y) = (markers[0], markers[1]);
        var a = Assert.NotNull((await rerun.PseudonymizedAsync(PseudonymizedTexts.Holding(x))).ItemId);
        var b = Assert.NotNull((await rerun.PseudonymizedAsync(PseudonymizedTexts.Holding(y), await rerun.AnotherBatchAsync())).ItemId);
        _ = await rerun.RequestAsync();
        var held = await rerun.TakenAsync();
        var seen = new List<RerunSource>();

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => rerun.Resanitizer.ResanitizeAsync(held, Remapping(_ => [Entry(x), Entry(y)], seen.Add), Token));

        Assert.Equal((RerunMarkers.Draws, true), (seen.Count, seen[^1].Taken.Contains(y)));
        Assert.Equal(($"{x}:false|{Joined(x, y)}", $"{y}:false|{Joined(x, y)}"), (await rerun.MarkersOfAsync(a), await rerun.MarkersOfAsync(b)));
    }

    [DatabaseFact]
    public async Task RerunDrawsAgainWithoutAMarkerAnotherItemOfItsBatchHolds()
    {
        var rerun = await database.CaseAsync();
        var markers = PseudonymizedTexts.Draw(3);
        var (x, y, z) = (markers[0], markers[1], markers[2]);
        var a = Assert.NotNull((await rerun.PseudonymizedAsync(PseudonymizedTexts.Holding(x))).ItemId);
        var b = Assert.NotNull((await rerun.PseudonymizedAsync(PseudonymizedTexts.Holding(y))).ItemId);
        _ = await rerun.RequestAsync();
        var seen = new List<RerunSource>();

        var result = await rerun.Resanitizer.ResanitizeAsync(await rerun.TakenAsync(), Remapping(source => source.Taken.Contains(y) ? [Entry(x), Entry(z)] : [Entry(x), Entry(y)], seen.Add), Token);

        Assert.Equal(RerunOutcome.Replaced, result.Outcome);
        Assert.Equal(["", y], seen.Select(source => string.Join(',', source.Taken)));
        Assert.Equal(($"{Joined($"{x}:false", $"{z}:false")}|{Joined(x, y, z)}", $"{y}:false|{Joined(x, y, z)}"), (await rerun.MarkersOfAsync(a), await rerun.MarkersOfAsync(b)));
    }

    [DatabaseFact]
    public async Task PseudonymizedItemRefusesANewSanitizedTextWithoutANewPseudonymizedOne()
    {
        var rerun = await database.CaseAsync();
        var item = Assert.NotNull((await rerun.PseudonymizedAsync(PseudonymizedTexts.Holding(PseudonymizedTexts.Draw(1)))).ItemId);
        var before = await rerun.SanitizedAsync(item);
        _ = await rerun.RequestAsync();
        var held = await rerun.TakenAsync();

        var refused = await Assert.ThrowsAsync<PostgresException>(() => rerun.Resanitizer.ResanitizeAsync(held, RerunCase.Rerunning(NewText), Token));

        Assert.Equal(("CE004", before), (refused.SqlState, await rerun.SanitizedAsync(item)));
    }

    /// <summary>The rerun to <see cref="NewText"/> that pseudonymizes the item again with the map the function gives for what it is given, keeping each source it was given.</summary>
    private static Func<RerunSource, Resanitized> Remapping(Func<RerunSource, NewMapEntry[]> map, Action<RerunSource>? seen = null) => source =>
    {
        seen?.Invoke(source);
        var entries = map(source);
        return RerunCase.Rerunning(NewText)(source) with { Pseudonymized = PseudonymizedTexts.PseudonymizedOf(entries.Select(entry => entry.Marker)), Map = entries };
    };

    private static NewMapEntry Entry(string marker, bool author = false) => new(marker, PseudonymizedTexts.OriginalOf(marker), author, OccurrenceDestination.Pseudonymize);

    private static string Joined(params string[] values) => string.Join(',', values.Order(StringComparer.Ordinal));
}
