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
        Assert.Equal(ItemTexts.Version, counts.SanitizationVersion);
        Assert.Equal([new BatchStepCount("destinations", 0, 0)], counts.Steps);
    }

    [DatabaseFact]
    public async Task BatchDetailCountsSetTheEditsApartFromTheItemsAndKeepToTheVersionTheyName()
    {
        await using var world = await IngestionWorld.CreateAsync();
        using var custody = SyntheticCustody.Holding(Labels);
        var batch = await OpenAsync(world, "conversation");
        const string RerunSteps = "destinations@1\nzqx_review@1";
        var rerun = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(RerunSteps)));
        foreach (var edits in new[] { 2, 0, 3 })
        {
            _ = await WriteAsync(world, custody, batch, [], ItemTexts.Sanitization("texto sanitizado zqx") with { Counts = [edits] });
        }

        _ = await WriteAsync(world, custody, batch, [], ItemTexts.Sanitization("texto sanitizado zqx") with { Version = rerun, Steps = RerunSteps, Counts = [7, 1], Untreated = [0, 0] });

        var counts = await new BatchCounts(world.DataSource).ReadAsync(IngestionWorld.Domain, batch, TestContext.Current.CancellationToken);

        Assert.Equal(rerun, counts.SanitizationVersion);
        Assert.Equal([new BatchStepCount("destinations", 7, 1), new BatchStepCount("zqx_review", 1, 1)], counts.Steps);
    }

    [DatabaseFact]
    public async Task BatchDetailCountsNameTheMostRecentVersionEvenWhenItSortsBeforeTheOlderOne()
    {
        await using var world = await IngestionWorld.CreateAsync();
        using var custody = SyntheticCustody.Holding(Labels);
        var batch = await OpenAsync(world, "conversation");
        const string NewerSteps = "destinations@1\nzqx_newer@18";
        var newer = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(NewerSteps)));
        _ = await WriteAsync(world, custody, batch, []);
        _ = await WriteAsync(world, custody, batch, [], ItemTexts.Sanitization("texto sanitizado zqx") with { Version = newer, Steps = NewerSteps, Counts = [1, 0], Untreated = [0, 0] });

        var counts = await new BatchCounts(world.DataSource).ReadAsync(IngestionWorld.Domain, batch, TestContext.Current.CancellationToken);

        Assert.True(string.CompareOrdinal(newer, ItemTexts.Version) < 0, "The newer version must sort before the older one.");
        Assert.Equal(newer, counts.SanitizationVersion);
    }

    [DatabaseFact]
    public async Task BatchDetailCountsTellTheSanitizedItemsOutOfTheItemsNotEmptied()
    {
        await using var world = await IngestionWorld.CreateAsync();
        using var custody = SyntheticCustody.Holding(Labels);
        var batch = await OpenAsync(world, "conversation");
        _ = await WriteAsync(world, custody, batch, []);
        _ = await WriteAsync(world, custody, batch, []);
        _ = await new ContentWriter(world.DataSource, custody.Content).AddItemTextAsync(IngestionWorld.Domain, batch, ItemTexts.Of("texto bruto zqx"), TestContext.Current.CancellationToken);
        var emptied = await WriteAsync(world, custody, batch, []);
        _ = await IngestionWorld.AcceptAsync(world, batch);
        _ = await new ContentEmptier(world.DataSource).EmptyAsync(
            new ItemEmptying(CustodyAuthor.Person(await world.PersonAsync("zqx-curator")), IngestionWorld.Domain, emptied, EmptyingMotive.ContractRequirement, "PROT-2026/0846"),
            TestContext.Current.CancellationToken);

        var counts = await new BatchCounts(world.DataSource).ReadAsync(IngestionWorld.Domain, batch, TestContext.Current.CancellationToken);

        Assert.Equal((2L, 3L), (counts.SanitizedItems, counts.Items));
    }

    [DatabaseFact]
    public async Task BatchDetailCountsAddWhatEachStepLeftUntreated()
    {
        await using var world = await IngestionWorld.CreateAsync();
        using var custody = SyntheticCustody.Holding(Labels);
        var batch = await OpenAsync(world, "conversation");
        _ = await WriteAsync(world, custody, batch, [], ItemTexts.Sanitization("texto sanitizado zqx") with { Untreated = [2] });
        _ = await WriteAsync(world, custody, batch, [], ItemTexts.Sanitization("texto sanitizado zqx") with { Untreated = [3] });

        var counts = await new BatchCounts(world.DataSource).ReadAsync(IngestionWorld.Domain, batch, TestContext.Current.CancellationToken);

        Assert.Equal(5L, Assert.Single(counts.Steps).Untreated);
    }

    private static StoredOccurrence Said(long item, int position) =>
        new(item, position, new string('a', 64), new string('b', 64), 20, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private static string Joined(IEnumerable<BatchCount> counts) => string.Join(',', counts.Select(count => $"{count.Code}:{count.Value}"));

    private static IReadOnlyList<ItemOccurrence> Decided(params OccurrenceDestination[] destinations) =>
        [.. destinations.Select((destination, index) => new ItemOccurrence(index * 4, (index * 4) + 3, "person", destination))];

    private static async Task<long> WriteAsync(
        BatchesWorld world, SyntheticCustody custody, long batch, IReadOnlyList<ItemOccurrence> occurrences, ItemSanitization? sanitization = null) =>
        await new ContentWriter(world.DataSource, custody.Content).AddItemTextAsync(
            IngestionWorld.Domain,
            batch,
            new NewItemText("texto bruto zqx", "texto pseudonimizado zqx", occurrences) { Sanitization = sanitization ?? ItemTexts.Sanitization("texto sanitizado zqx") },
            TestContext.Current.CancellationToken);

    private static async Task<long> OpenAsync(BatchesWorld world, string form)
    {
        var opened = await IngestionWorld.BatchesOf(world).OpenAsync(
            new BatchOpening(IngestionWorld.Domain, IngestionWorld.Person, null) { DeclaredForm = form }, TestContext.Current.CancellationToken);
        Assert.Equal(BatchOutcome.Opened, opened.Outcome);
        return opened.BatchId!.Value;
    }
}
