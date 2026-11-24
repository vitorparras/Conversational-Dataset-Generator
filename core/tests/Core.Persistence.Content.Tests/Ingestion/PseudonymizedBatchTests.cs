using Core.Persistence.Content;
using Core.Persistence.Content.Writing;
using Core.Persistence.Custody;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Content;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// The database holds the pseudonymized text to its path (issues 101 and 102): an item holds it only with a map of its own written in the same transaction, a batch holding it is accepted
/// only with the event of the pseudonymization in the transaction of the acceptance, and an entry of a span to remove never reaches the map: the batch is rejected with its motive.
/// </summary>
[Trait("Category", "Database")]
public sealed class PseudonymizedBatchTests
{
    private const string WithoutEvent = "CE005";
    private const string AcceptedEvent =
        "INSERT INTO public.custody_event (actor_system, item_kind, item_id, step, reason) VALUES (true, 'ingestion_batch', @event, 'batch_closed', 'batch_accepted')";
    private const string Accept = "UPDATE content.ingestion_batch SET state = 'accepted' WHERE id = @id";
    private const string Pseudonymized = "SELECT count(*) FROM content.item_text WHERE pseudonymized_text IS NOT NULL";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [DatabaseFact]
    public async Task PseudonymizedWithoutItsMapIsRefused()
    {
        await using var world = await ContentWorld.CreateAsync();
        var marker = PseudonymizedTexts.Draw(1)[0];
        var withoutMap = ItemTexts.Of(PseudonymizedTexts.Raw, PseudonymizedTexts.Sanitized, PseudonymizedTexts.PseudonymizedOf([marker]));

        _ = await Assert.ThrowsAsync<PseudonymizedRefusedException>(() => world.RealWriter.AddItemTextAsync(1, 1, withoutMap, Token));
        var refusedCount = (long)(await world.OwnerInDomainAsync(1, Pseudonymized))!;
        var written = await world.RealWriter.AddPseudonymizedItemAsync(1, 1, withoutMap with { Map = PseudonymizedTexts.Holding(marker).Map }, Token);

        Assert.Equal((0L, true, 1L), (refusedCount, written.ItemId is not null, (long)(await world.OwnerInDomainAsync(1, Pseudonymized))!));
    }

    [DatabaseFact]
    public async Task PseudonymizedWithoutTheEventIsRefused()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        Assert.NotNull((await world.RealWriter.AddPseudonymizedItemAsync(IngestionWorld.Domain, batch, PseudonymizedTexts.Holding(PseudonymizedTexts.Draw(1)), Token)).ItemId);
        await ReviewedBatches.ReviewCleanAsync(world.DataSource, IngestionWorld.Domain, batch, IngestionWorld.Person);

        // The closing event is there and the review is clean: what is missing is only the event of the pseudonymization, which the batches of production append with the acceptance.
        var refused = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAfterAsync(AcceptedEvent, IngestionWorld.Domain, Accept, ("event", batch), ("id", batch)));
        var accepted = await IngestionWorld.BatchesOf(world).AcceptAsync(new BatchClosing(IngestionWorld.Domain, batch, CustodyAuthor.System), Token);

        Assert.Equal((WithoutEvent, BatchOutcome.Accepted), (refused.SqlState, accepted.Outcome));
    }

    [DatabaseFact]
    public async Task AcceptanceRecordsThePseudonymizationInItsTransactionAndOnlyForAPseudonymizedBatch()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var plain = await IngestionWorld.OpenAsync(world);
        _ = await IngestionWorld.AddItemAsync(world, plain);
        var pseudonymized = await IngestionWorld.OpenAsync(world);
        Assert.NotNull((await world.RealWriter.AddPseudonymizedItemAsync(IngestionWorld.Domain, pseudonymized, PseudonymizedTexts.Holding(PseudonymizedTexts.Draw(1)), Token)).ItemId);

        var unreviewed = await IngestionWorld.BatchesOf(world).AcceptAsync(new BatchClosing(IngestionWorld.Domain, pseudonymized, CustodyAuthor.System), Token);
        var refusedEvents = await PseudonymizationEventsAsync(world);
        var accepted = new[] { await IngestionWorld.AcceptAsync(world, plain), await IngestionWorld.AcceptAsync(world, pseudonymized) };

        Assert.Equal((BatchOutcome.ReviewMissing, string.Empty), (unreviewed.Outcome, refusedEvents));
        Assert.Equal([BatchOutcome.Accepted, BatchOutcome.Accepted], accepted.Select(result => result.Outcome));
        Assert.Equal(
            $"{pseudonymized}:batch_pseudonymized:administrative_act:-;{pseudonymized}:batch_closed:batch_accepted:-",
            await world.ScalarAsync(
                "SELECT string_agg(item_id || ':' || step || ':' || reason || ':' || coalesce(reference, '-'), ';' ORDER BY sequence) FROM public.custody_event "
                + "WHERE item_kind = 'ingestion_batch' AND item_id = @batch AND step IN ('batch_pseudonymized', 'batch_closed')",
                ("batch", pseudonymized)));
        Assert.Equal(string.Empty, await PseudonymizationEventsOfAsync(world, plain));
    }

    [DatabaseFact]
    public async Task RemovedSpanIsRefusedFromTheMapAndTheAttemptIsRecorded()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        var markers = PseudonymizedTexts.Draw(2);
        var item = PseudonymizedTexts.WithMap(
        [
            new NewMapEntry(markers[0], PseudonymizedTexts.OriginalOf(markers[0]), false, OccurrenceDestination.Pseudonymize),
            new NewMapEntry(markers[1], "zqx-credencial-sintetica", false, OccurrenceDestination.Remove),
        ]);
        var closing = new BatchClosing(IngestionWorld.Domain, batch, CustodyAuthor.System);

        _ = await Assert.ThrowsAsync<ArgumentException>(() => world.RealWriter.AddPseudonymizedItemAsync(IngestionWorld.Domain, batch, item, Token));
        var added = await new PseudonymizedItems(world.RealWriter, IngestionWorld.BatchesOf(world)).AddAsync(closing, _ => item, Token);

        Assert.Equal(((long?)null, (BatchOutcome?)BatchOutcome.Rejected), (added.ItemId, added.Rejection?.Outcome));
        Assert.Equal(("0,0", 0L), ((string?)await world.ScalarAsync(IngestionWorld.Reachable), (long)(await world.ScalarAsync("SELECT count(*) FROM content.marker_in_use"))!));
        Assert.EndsWith("batch_closed:batch_rejected:pseudonym_of_removed", (string)(await world.ScalarAsync(IngestionWorld.Events))!, StringComparison.Ordinal);
    }

    private static async Task<string> PseudonymizationEventsAsync(ContentWorld world) =>
        (string?)await world.ScalarAsync("SELECT string_agg(item_id::text, ',') FROM public.custody_event WHERE step = 'batch_pseudonymized'") ?? string.Empty;

    private static async Task<string> PseudonymizationEventsOfAsync(ContentWorld world, long batch) =>
        (string?)await world.ScalarAsync("SELECT string_agg(item_id::text, ',') FROM public.custody_event WHERE step = 'batch_pseudonymized' AND item_id = @batch", ("batch", batch))
        ?? string.Empty;
}
