using Core.Persistence.Content;
using Core.Persistence.Content.Writing;
using Core.Persistence.Custody;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Content;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// The pseudonymized item is written with its map and its markers; a draw that lost to a marker held already is drawn again knowing the markers taken, a bounded number of times, and a map
/// with an entry that is not to pseudonymize rejects the batch (issue 101, CA-5; decision 0057).
/// </summary>
public sealed class PseudonymizedItemsTests
{
    private const string Markers = "SELECT string_agg(marker, ',' ORDER BY marker) FROM content.item_marker WHERE item_id = @id";

    private static readonly string[] Labels = ["texto-bruto/1/v1", "mapa-reversao/1/v1", "texto-bruto/2/v1", "mapa-reversao/2/v1"];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [DatabaseFact]
    public async Task MissingArgumentsAreRefusedByTheirNames()
    {
        await using var world = await IngestionWorld.CreateAsync();
        using var custody = SyntheticCustody.Holding(Labels);
        var writer = new ContentWriter(world.DataSource, custody.Content);
        var items = ItemsOf(world, custody);

        var withoutWriter = Assert.Throws<ArgumentNullException>(() => new PseudonymizedItems(null!, IngestionWorld.BatchesOf(world)));
        var withoutBatches = Assert.Throws<ArgumentNullException>(() => new PseudonymizedItems(writer, null!));
        var withoutBatch = await Assert.ThrowsAsync<ArgumentNullException>(() => items.AddAsync(null!, _ => Holding(ItemTexts.Marker()), Token));
        var withoutSwap = await Assert.ThrowsAsync<ArgumentNullException>(() => items.AddAsync(ClosingOf(1), null!, Token));

        Assert.Equal(
            ("writer", "batches", "batch", "pseudonymize"),
            (withoutWriter.ParamName, withoutBatches.ParamName, withoutBatch.ParamName, withoutSwap.ParamName));
    }

    [DatabaseFact]
    public async Task ItemWhoseEveryEntryIsToPseudonymizeIsWrittenWithItsMarkers()
    {
        await using var world = await IngestionWorld.CreateAsync();
        using var custody = SyntheticCustody.Holding(Labels);
        var batch = await IngestionWorld.OpenAsync(world);
        var marker = ItemTexts.Marker();

        var added = await ItemsOf(world, custody).AddAsync(ClosingOf(batch), _ => Holding(marker), Token);

        Assert.Null(added.Rejection);
        Assert.Equal(marker, await world.InDomainAsync(IngestionWorld.Domain, Markers, ("id", Assert.NotNull(added.ItemId))));
    }

    [DatabaseFact]
    public async Task DrawThatLostIsDrawnAgainKnowingTheMarkerTaken()
    {
        await using var world = await IngestionWorld.CreateAsync();
        using var custody = SyntheticCustody.Holding(Labels);
        var batch = await IngestionWorld.OpenAsync(world);
        var (held, free) = (ItemTexts.Marker(), ItemTexts.Marker());
        var items = ItemsOf(world, custody);
        _ = Assert.NotNull((await items.AddAsync(ClosingOf(batch), _ => Holding(held), Token)).ItemId);
        var seen = new List<string>();

        var added = await items.AddAsync(ClosingOf(batch), taken =>
        {
            seen.Add(Joined(taken));
            return Holding(taken.Contains(held) ? free : held);
        }, Token);

        Assert.Equal(["", held], seen);
        Assert.Equal(free, await world.InDomainAsync(IngestionWorld.Domain, Markers, ("id", Assert.NotNull(added.ItemId))));
    }

    [DatabaseFact]
    public async Task DrawsThatAllLoseEndAfterTheEighthWithNothingWritten()
    {
        await using var world = await IngestionWorld.CreateAsync();
        using var custody = SyntheticCustody.Holding(Labels);
        var batch = await IngestionWorld.OpenAsync(world);
        var held = ItemTexts.Marker();
        var items = ItemsOf(world, custody);
        _ = Assert.NotNull((await items.AddAsync(ClosingOf(batch), _ => Holding(held), Token)).ItemId);
        var seen = new List<string>();

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => items.AddAsync(ClosingOf(batch), taken =>
        {
            seen.Add(Joined(taken));
            return Holding(held);
        }, Token));

        Assert.Equal("Every draw of the markers lost to markers held already.", refused.Message);
        Assert.Equal(8, seen.Count);
        // The row policy hides the items of a batch not yet accepted from the application, so the count is the owner's.
        Assert.Equal(1L, await world.AsSuperuserAsync("SELECT count(*) FROM content.item_text WHERE batch_id = @id", ("id", batch)));
    }

    private static PseudonymizedItems ItemsOf(BatchesWorld world, SyntheticCustody custody) =>
        new(new ContentWriter(world.DataSource, custody.Content), IngestionWorld.BatchesOf(world));

    private static BatchClosing ClosingOf(long batch) => new(IngestionWorld.Domain, batch, CustodyAuthor.System);

    /// <summary>The markers the draw knows as taken, in order.</summary>
    private static string Joined(IEnumerable<string> taken) => string.Join(',', taken.Order(StringComparer.Ordinal));

    private static NewItemText Holding(string marker) =>
        new("texto bruto zqx", $"{marker} disse algo sintetico zqx", [])
        {
            Sanitization = ItemTexts.Sanitization("texto sanitizado zqx"),
            Map = [new NewMapEntry(marker, "original sintetico zqx", false, OccurrenceDestination.Pseudonymize)],
        };
}
