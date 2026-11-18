using Core.Persistence.Content;
using Core.Persistence.Content.Emptying;
using Core.Persistence.Content.Writing;
using Core.Persistence.Custody;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Content;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// The counts of a batch for its detail (issue 821): how many passages each item sends to each destination, kept in clear with the item and never changed but by the emptying, which zeroes them
/// (migration 0056); and the numbers of a batch still open, read by a function that answers numbers and codes alone (migration 0057).
/// </summary>
public sealed class BatchDetailCountsTests
{
    private const string Passages = "SELECT removed_passages || ',' || pseudonymized_passages || ',' || preserved_passages FROM content.item_text WHERE id = @id";

    private static readonly string[] Labels = ["texto-bruto/1/v1", "mapa-reversao/1/v1", "texto-bruto/2/v1", "mapa-reversao/2/v1"];

    [DatabaseFact]
    public async Task DestinationCountsCannotChange()
    {
        await using var world = await IngestionWorld.CreateAsync();
        using var custody = SyntheticCustody.Holding(Labels);
        var batch = await OpenAsync(world, "conversation");
        var item = await WriteAsync(world, custody, batch, Decided(OccurrenceDestination.Remove, OccurrenceDestination.Remove, OccurrenceDestination.Pseudonymize));

        var refused = await Assert.ThrowsAsync<PostgresException>(() => world.AsSuperuserAsync("UPDATE content.item_text SET removed_passages = 0 WHERE id = @id", ("id", item)));

        Assert.Equal(("CE004", "2,1,0"), (refused.SqlState, await world.AsSuperuserAsync(Passages, ("id", item))));
    }

    [DatabaseFact]
    public async Task EmptyingZeroesTheDestinationCounts()
    {
        await using var world = await IngestionWorld.CreateAsync();
        using var custody = SyntheticCustody.Holding(Labels);
        var batch = await OpenAsync(world, "conversation");
        var item = await WriteAsync(world, custody, batch, Decided(OccurrenceDestination.Preserve, OccurrenceDestination.Pseudonymize));
        _ = await IngestionWorld.AcceptAsync(world, batch);
        var curator = await world.PersonAsync("zqx-curator");

        var emptied = await new ContentEmptier(world.DataSource).EmptyAsync(
            new ItemEmptying(CustodyAuthor.Person(curator), IngestionWorld.Domain, item, EmptyingMotive.RemovalRequestedBySubject, "PROT-2026/0821"), TestContext.Current.CancellationToken);

        Assert.Equal((EmptyingOutcome.Emptied, "0,0,0"), (emptied.Outcome, await world.AsSuperuserAsync(Passages, ("id", item))));
    }

    [DatabaseFact]
    public async Task BatchDetailCountsOfAnOpenBatchReturnOnlyNumbers()
    {
        await using var world = await IngestionWorld.CreateAsync();
        using var custody = SyntheticCustody.Holding(Labels);
        var batch = await OpenAsync(world, "conversation");
        _ = await WriteAsync(world, custody, batch, Decided(OccurrenceDestination.Remove, OccurrenceDestination.Pseudonymize, OccurrenceDestination.Pseudonymize));
        _ = await WriteAsync(world, custody, batch, Decided(OccurrenceDestination.Preserve));
        var accommodated = new NewItemText("texto bruto zqx", new ItemAccommodation("document", ["author", "thread"]));
        var third = await new ContentWriter(world.DataSource, custody.Content).AddItemTextAsync(IngestionWorld.Domain, batch, accommodated, TestContext.Current.CancellationToken);
        await new ReplicationStore(world.DataSource).RecordAsync(
            new ReplicationTarget(IngestionWorld.Domain, batch),
            [Said(third, 0), Said(third, 1), Said(third, 2)],
            [new StoredMark(third, 0, third, 1), new StoredMark(third, 2, third, 1)],
            TestContext.Current.CancellationToken);

        var counts = await new BatchCounts(world.DataSource).ReadAsync(IngestionWorld.Domain, batch, TestContext.Current.CancellationToken);
        var shape = await world.AsSuperuserAsync("SELECT pg_catalog.pg_get_function_result('public.batch_detail_counts(bigint, integer)'::regprocedure)");

        Assert.Equal(
            ("conversation:2,document:1", "remove:1,pseudonymize:2,preserve:1", "2,1", "document:author,thread:1"),
            (Joined(counts.Forms), Joined(counts.Destinations), $"{counts.Marked},{counts.Firsts}", Joined(counts.Accommodations)));
        Assert.Equal("TABLE(count_kind text, count_code text, count_value bigint)", shape);
    }

    private static StoredOccurrence Said(long item, int position) =>
        new(item, position, new string('a', 64), new string('b', 64), 20, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private static string Joined(IEnumerable<BatchCount> counts) => string.Join(',', counts.Select(count => $"{count.Code}:{count.Value}"));

    private static IReadOnlyList<ItemOccurrence> Decided(params OccurrenceDestination[] destinations) =>
        [.. destinations.Select((destination, index) => new ItemOccurrence(index * 4, (index * 4) + 3, "person", destination))];

    private static async Task<long> WriteAsync(BatchesWorld world, SyntheticCustody custody, long batch, IReadOnlyList<ItemOccurrence> occurrences) =>
        await new ContentWriter(world.DataSource, custody.Content).AddItemTextAsync(
            IngestionWorld.Domain, batch, new NewItemText("texto bruto zqx", "texto sanitizado zqx", "texto pseudonimizado zqx", occurrences), TestContext.Current.CancellationToken);

    private static async Task<long> OpenAsync(BatchesWorld world, string form)
    {
        var opened = await IngestionWorld.BatchesOf(world).OpenAsync(
            new BatchOpening(IngestionWorld.Domain, IngestionWorld.Person, null) { DeclaredForm = form }, TestContext.Current.CancellationToken);
        Assert.Equal(BatchOutcome.Opened, opened.Outcome);
        return opened.BatchId!.Value;
    }
}
