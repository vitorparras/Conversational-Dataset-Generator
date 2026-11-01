using Core.Persistence.Content;
using Core.Persistence.Custody;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// The map of the content tables against the real server (#60, part 2): each read of protected content is seen by the server and recorded in the
/// chain in the same number, and a read that skips the point of reading shows as a difference, which is how the forgotten path is found.
/// </summary>
[Trait("Category", "Database")]
public sealed class ContentAuditFidelityTests
{
    [DatabaseFact]
    public async Task ReadsThroughThePointOfReadingAreSeenAsManyTimesAsTheChainRecordsThem()
    {
        var from = DateTimeOffset.UtcNow.AddMinutes(-1);
        var token = TestContext.Current.CancellationToken;
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText("texto bruto sintetico zqx", "texto sanitizado sintetico zqx"), token);
        var map = await world.Writer.AddReversalMapAsync(1, item, "mapa sintetico zqx", token);
        _ = await world.Reader.ReadAsync(new ContentRead(CustodyAuthor.System, ContentColumns.RawText, 1, item), token);
        _ = await world.Reader.ReadAsync(new ContentRead(CustodyAuthor.System, ContentColumns.SanitizedText, 1, item), token);
        _ = await world.Reader.ReadAsync(new ContentRead(CustodyAuthor.System, ContentColumns.Entries, 1, map), token);

        var differences = await AuditFidelity.DifferencesAsync(world.DataSource, from);

        Assert.DoesNotContain(differences, difference => difference.Role == "app");
    }

    [DatabaseFact]
    public async Task ReadThatSkipsThePointOfReadingIsADifferenceNamingTheTable()
    {
        var from = DateTimeOffset.UtcNow.AddMinutes(-1);
        var token = TestContext.Current.CancellationToken;
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText("texto bruto sintetico zqx"), token);
        _ = await world.Reader.ReadAsync(new ContentRead(CustodyAuthor.System, ContentColumns.RawText, 1, item), token);
        _ = await world.InDomainAsync(1, "SELECT raw_text FROM content.item_text WHERE id = @id", ("id", item));

        var differences = await AuditFidelity.DifferencesAsync(world.DataSource, from);

        var difference = Assert.Single(differences, found => found.Role == "app");
        Assert.Equal(("content.item_text", 2, 1, null), (difference.Table, difference.Seen, difference.Recorded, difference.KnownGap));
    }
}
