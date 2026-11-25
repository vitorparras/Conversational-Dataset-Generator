using Core.Persistence.Content;
using Core.Persistence.Content.Emptying;
using Core.Persistence.Content.Writing;
using Core.Persistence.Custody;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;
using Core.Persistence.Tests.Ingestion;
using Npgsql;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// The markers in clear (issue 101): each held once in the installation and never released, given to the item with its map in one transaction, emptied with the item while staying held,
/// and a draw that loses to another domain drawn again without the row that won it ever being read.
/// </summary>
[Trait("Category", "Database")]
public sealed class MarkerStorageTests
{
    private const string UniqueViolation = "23505";
    private const string WithoutEvent = "CE005";
    private const string Held = "SELECT coalesce(string_agg(marker, ',' ORDER BY marker), '') FROM content.marker_in_use";
    private const string OfItem = "SELECT coalesce(string_agg(marker, ',' ORDER BY marker), '') FROM content.item_marker WHERE item_id = @id";
    private const string OfItemWithAuthor = "SELECT string_agg(marker || ':' || author, ',' ORDER BY marker) FROM content.item_marker WHERE item_id = @id";
    private const string Items = "SELECT count(*) || ',' || (SELECT count(*) FROM content.reversal_map) FROM content.item_text";

    private const string AddedLater = "WITH held AS (INSERT INTO content.marker_in_use (domain_id, marker, kind) VALUES (1, @marker, 'person') RETURNING marker) "
        + "INSERT INTO content.item_marker (domain_id, item_id, marker, author) SELECT 1, @id, marker, false FROM held";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static NewMapEntry Entry(string marker, bool author) => new(marker, PseudonymizedTexts.OriginalOf(marker), author, OccurrenceDestination.Pseudonymize);

    [DatabaseFact]
    public async Task MarkerIsUniqueInTheInstallation()
    {
        await using var world = await ContentWorld.CreateAsync();
        var marker = PseudonymizedTexts.Draw(1)[0];
        var first = await world.RealWriter.AddPseudonymizedItemAsync(1, 1, PseudonymizedTexts.Holding(marker), Token);

        var second = await world.RealWriter.AddPseudonymizedItemAsync(2, 2, PseudonymizedTexts.Holding(marker), Token);
        var byHand = await Assert.ThrowsAsync<PostgresException>(
            () => world.OwnerInDomainAsync(2, "INSERT INTO content.marker_in_use (domain_id, marker, kind) VALUES (2, @marker, 'person')", ("marker", marker)));

        Assert.Equal((true, null, marker), (first.ItemId is not null, second.ItemId, Assert.Single(second.Taken)));
        Assert.Equal((UniqueViolation, "0,0", marker), (byHand.SqlState, (string?)await world.InDomainAsync(2, Items), (string?)await world.InDomainAsync(1, Held)));
    }

    [DatabaseFact]
    public async Task MarkerIsHeldAsAnAuthorWhenAnyOfItsOccurrencesStandsForOne()
    {
        await using var world = await ContentWorld.CreateAsync();
        var markers = PseudonymizedTexts.Draw(2);
        var (author, other) = (markers[0], markers[1]);
        var item = PseudonymizedTexts.WithMap([Entry(author, false), Entry(author, true), Entry(other, false)]);

        var written = await world.RealWriter.AddPseudonymizedItemAsync(1, 1, item, Token);

        Assert.Equal(
            string.Join(',', new[] { $"{author}:true", $"{other}:false" }.Order(StringComparer.Ordinal)),
            (string?)await world.InDomainAsync(1, OfItemWithAuthor, ("id", Assert.NotNull(written.ItemId))));
    }

    [DatabaseFact]
    public async Task DrawLosingToAnotherDomainDrawsAgainWithoutReadingIt()
    {
        await using var world = await ContentWorld.CreateAsync();
        var markers = PseudonymizedTexts.Draw(2);
        var (lost, won) = (markers[0], markers[1]);
        _ = Assert.NotNull((await world.RealWriter.AddPseudonymizedItemAsync(1, 1, PseudonymizedTexts.Holding(lost), Token)).ItemId);
        var given = new List<string>();

        var added = await new PseudonymizedItems(world.RealWriter, IngestionWorld.BatchesOf(world)).AddAsync(
            new BatchClosing(2, 2, CustodyAuthor.System),
            taken =>
            {
                given.Add(string.Join(',', taken));
                return PseudonymizedTexts.Holding(taken.Contains(lost) ? won : lost);
            },
            Token);

        // The second domain sees only the marker it holds: it learned that the first was taken from the index refusing it, never from the row of the other domain.
        var item = Assert.NotNull(added.ItemId);
        Assert.Equal([string.Empty, lost], given);
        Assert.Equal((won, won, "1,1"), ((string?)await world.InDomainAsync(2, Held), (string?)await world.InDomainAsync(2, OfItem, ("id", item)), (string?)await world.InDomainAsync(2, Items)));
    }

    [DatabaseFact]
    public async Task ItemMarkersAreEmptiedWithTheItem()
    {
        await using var world = await ContentWorld.CreateAsync();
        var markers = PseudonymizedTexts.Draw(2);
        var item = Assert.NotNull((await world.RealWriter.AddPseudonymizedItemAsync(1, 1, PseudonymizedTexts.Holding(markers), Token)).ItemId);
        var expected = string.Join(',', markers.Order(StringComparer.Ordinal));
        var curator = await world.PersonAsync("zqx-curator");

        var outside = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, "DELETE FROM content.item_marker WHERE item_id = @id", ("id", item)));
        var added = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, AddedLater, ("id", item), ("marker", PseudonymizedTexts.Draw(1)[0])));
        var before = (string?)await world.InDomainAsync(1, OfItem, ("id", item));
        var emptied = await world.Emptier.EmptyAsync(new ItemEmptying(CustodyAuthor.Person(curator), 1, item, EmptyingMotive.ContractRequirement, "PROT-2026/0846"), Token);

        Assert.Equal((WithoutEvent, WithoutEvent, expected), (outside.SqlState, added.SqlState, before));
        Assert.Equal((EmptyingOutcome.Emptied, string.Empty, expected), (emptied.Outcome, (string?)await world.InDomainAsync(1, OfItem, ("id", item)), (string?)await world.InDomainAsync(1, Held)));
    }

    [DatabaseFact]
    public async Task HeldMarkerIsNeverReleased()
    {
        await using var world = await ContentWorld.CreateAsync();
        var marker = PseudonymizedTexts.Draw(1)[0];
        _ = Assert.NotNull((await world.RealWriter.AddPseudonymizedItemAsync(1, 1, PseudonymizedTexts.Holding(marker), Token)).ItemId);

        var deleted = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, "DELETE FROM content.marker_in_use WHERE marker = @marker", ("marker", marker)));
        var changed = await Assert.ThrowsAsync<PostgresException>(
            () => world.OwnerInDomainAsync(1, "UPDATE content.marker_in_use SET kind = kind WHERE marker = @marker", ("marker", marker)));

        Assert.Equal(("CE006", "CE006", marker), (deleted.SqlState, changed.SqlState, (string?)await world.InDomainAsync(1, Held)));
    }
}
