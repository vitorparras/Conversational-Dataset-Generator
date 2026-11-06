using Core.Persistence.Content.Writing;
using Core.Persistence.Custody;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Content;

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
    public static Task<ContentWorld> CreateAsync() => ContentWorld.CreateAsync(seeded: false);

    /// <summary>The batches over the application's credential of the world.</summary>
    public static IngestionBatches BatchesOf(ContentWorld world) => new(world.DataSource);

    /// <summary>Opens a batch in the domain of the tests and answers its identifier, after judging that it opened.</summary>
    public static async Task<long> OpenAsync(ContentWorld world)
    {
        var opened = await BatchesOf(world).OpenAsync(Domain, Person, TestContext.Current.CancellationToken);
        Assert.Equal(BatchOutcome.Opened, opened.Outcome);
        return opened.BatchId!.Value;
    }

    /// <summary>Adds an item to the batch, as the system does, and answers its identifier.</summary>
    public static Task<long> AddItemAsync(ContentWorld world, long batch) =>
        world.RealWriter.AddItemTextAsync(Domain, batch, new NewItemText("texto bruto sintetico zqx"), TestContext.Current.CancellationToken);

    /// <summary>Adds the map of the item to the batch, as the system does, and answers its identifier.</summary>
    public static Task<long> AddMapAsync(ContentWorld world, long batch, long item) =>
        world.RealWriter.AddReversalMapAsync(Domain, new NewReversalMap(batch, item, "mapa sintetico zqx"), TestContext.Current.CancellationToken);

    /// <summary>The batch of the domain of the tests, closed by the system as accepted.</summary>
    public static Task<BatchResult> AcceptAsync(ContentWorld world, long batch) =>
        BatchesOf(world).AcceptAsync(new BatchClosing(Domain, batch, CustodyAuthor.System), TestContext.Current.CancellationToken);

    /// <summary>The batch of the domain of the tests, closed by the system as rejected for the motive.</summary>
    public static Task<BatchResult> RejectAsync(ContentWorld world, long batch, string motive) =>
        BatchesOf(world).RejectAsync(new BatchClosing(Domain, batch, CustodyAuthor.System), motive, TestContext.Current.CancellationToken);
}
