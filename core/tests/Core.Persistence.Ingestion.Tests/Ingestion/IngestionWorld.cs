using Core.Persistence.Custody;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>The batches over a world that holds none, as the system has them: every write goes through the real writer and the real batches, and the database judges.</summary>
internal static class IngestionWorld
{
    /// <summary>The domain the tests of the batches work in.</summary>
    public const int Domain = 1;

    /// <summary>The person, who is active, that opens the batches.</summary>
    public const long Person = 1;

    /// <summary>The events of the chain, in order, as <c>step:reason:reference</c>, with a dash where there is no reference.</summary>
    public const string Events = "SELECT string_agg(step || ':' || reason || ':' || coalesce(reference, '-'), ',' ORDER BY sequence) FROM public.custody_event";

    /// <summary>How many items and maps the domain declared can read, as <c>items,maps</c>.</summary>
    public const string Reachable = "SELECT (SELECT count(*) FROM content.item_text) || ',' || (SELECT count(*) FROM content.reversal_map)";

    /// <summary>A world with the domains, the people and the chain, and no batch at all.</summary>
    public static Task<BatchesWorld> CreateAsync() => BatchesWorld.CreateAsync();

    /// <summary>The batches over the application's credential of the world.</summary>
    public static IngestionBatches BatchesOf(BatchesWorld world) => new(world.DataSource, TestConditions.Known);

    /// <summary>Opens a batch in the domain of the tests, with no condition declared, and answers its identifier, after judging that it opened.</summary>
    public static Task<long> OpenAsync(BatchesWorld world) => OpenAsync(world, null);

    /// <summary>Opens a batch in the domain of the tests, under the condition if one is declared, and answers its identifier, after judging that it opened.</summary>
    public static async Task<long> OpenAsync(BatchesWorld world, string? condition)
    {
        var opened = await BatchesOf(world).OpenAsync(new BatchOpening(Domain, Person, condition), TestContext.Current.CancellationToken);
        Assert.Equal(BatchOutcome.Opened, opened.Outcome);
        return opened.BatchId!.Value;
    }

    /// <summary>Adds an item to the batch with a statement of its own, which the batch rules judge as they judge the writer of the system, and answers its identifier.</summary>
    public static Task<long> AddItemAsync(BatchesWorld world, long batch) => AddItemAsync(world, Domain, batch);

    /// <summary>Adds an item to the batch of the domain, as <see cref="AddItemAsync(BatchesWorld, long)"/> does for the domain of the tests.</summary>
    public static async Task<long> AddItemAsync(BatchesWorld world, int domain, long batch) =>
        (long)(await world.InDomainAsync(
            domain,
            "INSERT INTO content.item_text (domain_id, batch_id, raw_text, dek_epoch, dek_wrapped) "
            + "VALUES (@domain, @batch, decode('a1', 'hex'), 1, decode('c3', 'hex')) RETURNING id",
            ("domain", domain),
            ("batch", batch)))!;

    /// <summary>Opens a batch of the domain for a file whose digest is the digit repeated, and answers its identifier, after judging that it opened.</summary>
    public static async Task<long> OpenFileAsync(BatchesWorld world, int domain, char digit)
    {
        var receipt = new SubmissionReceipt(new string(digit, 32), 10, new string(digit, 64));
        var opened = await BatchesOf(world).OpenAsync(new BatchOpening(domain, Person, null, new SubmissionOpening(receipt, OriginDeclaration.Unknown)), TestContext.Current.CancellationToken);
        Assert.Equal(BatchOutcome.Opened, opened.Outcome);
        return opened.BatchId!.Value;
    }

    /// <summary>The batch of the domain, closed by the system as accepted.</summary>
    public static Task<BatchResult> AcceptInAsync(BatchesWorld world, int domain, long batch) =>
        BatchesOf(world).AcceptAsync(new BatchClosing(domain, batch, CustodyAuthor.System), TestContext.Current.CancellationToken);

    /// <summary>Adds the map of the item to the batch with a statement of its own, and answers its identifier.</summary>
    public static async Task<long> AddMapAsync(BatchesWorld world, long batch, long item) =>
        (long)(await world.InDomainAsync(
            Domain,
            "INSERT INTO content.reversal_map (domain_id, batch_id, item_id, entries, dek_epoch, dek_wrapped) "
            + "VALUES (@domain, @batch, @item, decode('b2', 'hex'), 1, decode('c3', 'hex')) RETURNING id",
            ("domain", Domain),
            ("batch", batch),
            ("item", item)))!;

    /// <summary>The batch of the domain of the tests, closed by the system as accepted.</summary>
    public static Task<BatchResult> AcceptAsync(BatchesWorld world, long batch) =>
        BatchesOf(world).AcceptAsync(new BatchClosing(Domain, batch, CustodyAuthor.System), TestContext.Current.CancellationToken);

    /// <summary>The batch of the domain of the tests, closed by the system as rejected for the motive.</summary>
    public static Task<BatchResult> RejectAsync(BatchesWorld world, long batch, string motive) =>
        BatchesOf(world).RejectAsync(new BatchClosing(Domain, batch, CustodyAuthor.System), motive, TestContext.Current.CancellationToken);
}
