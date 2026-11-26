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
/// and a draw that loses to another domain drawn again without the row that won it ever being read. The marker is held by the batch of its conversation (issue 102, 0057): every message of
/// the batch takes it, and no item of another batch, of the same domain or not, ever does.
/// </summary>
[Trait("Category", "Database")]
public sealed class MarkerStorageTests
{
    private const string UniqueViolation = "23505";
    private const string WithoutEvent = "CE005";
    private const string Held = "SELECT coalesce(string_agg(marker, ',' ORDER BY marker), '') FROM content.marker_in_use";
    private const string HeldByBatch = "SELECT string_agg(batch_id || ':' || marker, ',' ORDER BY batch_id, marker) FROM content.marker_in_use";
    private const string OfItem = "SELECT coalesce(string_agg(marker, ',' ORDER BY marker), '') FROM content.item_marker WHERE item_id = @id";
    private const string OfItemWithAuthor = "SELECT string_agg(marker || ':' || author, ',' ORDER BY marker) FROM content.item_marker WHERE item_id = @id";
    private const string Items = "SELECT count(*) || ',' || (SELECT count(*) FROM content.reversal_map) FROM content.item_text";

    private const string AddedLater = "WITH held AS (INSERT INTO content.marker_in_use (domain_id, batch_id, marker, kind) VALUES (1, 1, @marker, 'person') RETURNING marker) "
        + "INSERT INTO content.item_marker (domain_id, item_id, marker, author) SELECT 1, @id, marker, false FROM held";

    private const string AnotherBatch = "ALTER TABLE content.ingestion_batch DISABLE TRIGGER batch_is_born_open; ALTER TABLE content.ingestion_batch DISABLE TRIGGER batch_open_needs_event; "
        + "INSERT INTO content.ingestion_batch (domain_id, state, opened_by, condition, closed_at) SELECT 1, 'accepted', min(id), 'restrita', now() FROM public.person; "
        + "ALTER TABLE content.ingestion_batch ENABLE ALWAYS TRIGGER batch_is_born_open; ALTER TABLE content.ingestion_batch ENABLE ALWAYS TRIGGER batch_open_needs_event";

    private const string Resanitized = "INSERT INTO public.custody_event (actor_system, item_kind, item_id, step, reason, reference) "
        + "VALUES (true, 'item_text', @id, 'item_resanitized', 'sanitization_replaced', repeat('0a', 32))";

    private const string GivenByHand = "INSERT INTO content.item_marker (domain_id, item_id, marker, author) VALUES (1, @id, @marker, false)";

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
            () => world.OwnerInDomainAsync(2, "INSERT INTO content.marker_in_use (domain_id, batch_id, marker, kind) VALUES (2, 2, @marker, 'person')", ("marker", marker)));

        Assert.Equal((true, null, marker), (first.ItemId is not null, second.ItemId, Assert.Single(second.Taken)));
        Assert.Equal((UniqueViolation, "0,0", marker), (byHand.SqlState, (string?)await world.InDomainAsync(2, Items), (string?)await world.InDomainAsync(1, Held)));
    }

    [DatabaseFact]
    public async Task MessagesOfOneBatchShareTheMarkerItHolds()
    {
        await using var world = await ContentWorld.CreateAsync();
        var marker = PseudonymizedTexts.Draw(1)[0];

        var first = await world.RealWriter.AddPseudonymizedItemAsync(1, 1, PseudonymizedTexts.Holding(marker), Token);
        var second = await world.RealWriter.AddPseudonymizedItemAsync(1, 1, PseudonymizedTexts.Holding(marker), Token);

        Assert.Equal(
            (marker, marker, marker),
            ((string?)await world.InDomainAsync(1, OfItem, ("id", Assert.NotNull(first.ItemId))), (string?)await world.InDomainAsync(1, OfItem, ("id", Assert.NotNull(second.ItemId))),
                (string?)await world.InDomainAsync(1, Held)));
    }

    [DatabaseFact]
    public async Task MarkerOfAnotherBatchOfTheDomainIsTaken()
    {
        await using var world = await ContentWorld.CreateAsync();
        var marker = PseudonymizedTexts.Draw(1)[0];
        var other = await AnotherBatchAsync(world);
        _ = Assert.NotNull((await world.RealWriter.AddPseudonymizedItemAsync(1, 1, PseudonymizedTexts.Holding(marker), Token)).ItemId);

        var second = await world.RealWriter.AddPseudonymizedItemAsync(1, other, PseudonymizedTexts.Holding(marker), Token);

        Assert.Equal((null, marker, "1,1"), (second.ItemId, Assert.Single(second.Taken), (string?)await world.InDomainAsync(1, Items)));
    }

    [DatabaseFact]
    public async Task ReservationMetByAMarkerOfAnotherBatchHoldsNoneOfItsMarkers()
    {
        await using var world = await ContentWorld.CreateAsync();
        var markers = PseudonymizedTexts.Draw(3);
        var other = await AnotherBatchAsync(world);
        var first = await world.RealWriter.ReserveMarkersAsync(1, 1, [markers[0]], Token);

        var taken = await world.RealWriter.ReserveMarkersAsync(1, other, [markers[1], markers[0]], Token);
        var again = await world.RealWriter.ReserveMarkersAsync(1, other, [markers[1], markers[2]], Token);

        string[] held = [$"1:{markers[0]}", .. new[] { markers[1], markers[2] }.Order(StringComparer.Ordinal).Select(marker => $"{other}:{marker}")];
        Assert.Equal((0, markers[0], 0), (first.Count, Assert.Single(taken), again.Count));
        Assert.Equal(string.Join(',', held), (string?)await world.InDomainAsync(1, HeldByBatch));
    }

    [DatabaseFact]
    public async Task MarkerNewToAnItemRerunIsOneItsBatchHolds()
    {
        await using var world = await ContentWorld.CreateAsync();
        var markers = PseudonymizedTexts.Draw(3);
        var other = await AnotherBatchAsync(world);
        _ = Assert.NotNull((await world.RealWriter.AddPseudonymizedItemAsync(1, 1, PseudonymizedTexts.Holding(markers[0]), Token)).ItemId);
        _ = Assert.NotNull((await world.RealWriter.AddPseudonymizedItemAsync(1, other, PseudonymizedTexts.Holding(markers[1]), Token)).ItemId);
        var item = Assert.NotNull((await world.RealWriter.AddPseudonymizedItemAsync(1, other, PseudonymizedTexts.Holding(markers[2]), Token)).ItemId);

        var elsewhere = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAfterAsync(Resanitized, 1, GivenByHand, ("id", item), ("marker", markers[0])));
        _ = await world.OwnerInDomainAfterAsync(Resanitized, 1, GivenByHand, ("id", item), ("marker", markers[1]));

        Assert.Equal((WithoutEvent, "a marker new to an item is one its batch holds"), (elsewhere.SqlState, elsewhere.MessageText));
        Assert.Equal(string.Join(',', markers[1..].Order(StringComparer.Ordinal)), (string?)await world.InDomainAsync(1, OfItem, ("id", item)));
    }

    [DatabaseFact]
    public async Task ItemHoldsOneMapNotEmptied()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = Assert.NotNull((await world.RealWriter.AddPseudonymizedItemAsync(1, 1, PseudonymizedTexts.Holding(PseudonymizedTexts.Draw(1)[0]), Token)).ItemId);

        var second = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(
            1, "INSERT INTO content.reversal_map (domain_id, batch_id, item_id, entries, dek_epoch, dek_wrapped) VALUES (1, 1, @id, '\\x01', 1, '\\x01')", ("id", item)));

        Assert.Equal((UniqueViolation, "reversal_map_one_live"), (second.SqlState, second.ConstraintName));
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

    /// <summary>A second batch of the first domain, accepted, seeded by the owner as the batch of every domain is.</summary>
    private static async Task<long> AnotherBatchAsync(ContentWorld world) =>
        (long)(await world.OwnerInDomainAfterAsync(AnotherBatch, 1, "SELECT max(id) FROM content.ingestion_batch WHERE domain_id = 1"))!;
}
