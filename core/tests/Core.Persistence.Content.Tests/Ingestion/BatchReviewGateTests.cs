using Core.Persistence.Content.Writing;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Content;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// The sample is the database's: once drawn it neither changes nor lets another item in, the owner included, and the draw has no statement of its own that skips the chain (issue 83, CA-1).
/// </summary>
[Trait("Category", "Database")]
public sealed class BatchReviewGateTests
{
    private const string WithoutEvent = "CE005";
    private const string OutsideTheRules = "CE006";
    private const string Rewrite = "UPDATE content.ingestion_batch SET review_sample = '1,2,3' WHERE id = @id";

    [DatabaseFact]
    public async Task TheSampleDrawnNeverChangesByAnyStatementAndNoneIsDrawnWithoutTheEvent()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var drawn = await IngestionWorld.OpenAsync(world);
        var undrawn = await IngestionWorld.OpenAsync(world);
        _ = await IngestionWorld.AddItemAsync(world, drawn);
        _ = await new BatchReviews(world.DataSource, 1).DrawAsync(new BatchSampling(IngestionWorld.Domain, drawn, IngestionWorld.Person), TestContext.Current.CancellationToken);

        var rewritten = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, Rewrite, ("id", drawn)));
        var byOwner = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, Rewrite, ("id", drawn)));
        var withoutEvent = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, Rewrite, ("id", undrawn)));

        Assert.Equal((OutsideTheRules, OutsideTheRules, WithoutEvent), (rewritten.SqlState, byOwner.SqlState, withoutEvent.SqlState));
    }

    [DatabaseFact]
    public async Task NoItemEntersABatchOnceItsSampleWasDrawn()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        _ = await IngestionWorld.AddItemAsync(world, batch);
        _ = await new BatchReviews(world.DataSource, 1).DrawAsync(new BatchSampling(IngestionWorld.Domain, batch, IngestionWorld.Person), TestContext.Current.CancellationToken);

        _ = await Assert.ThrowsAsync<BatchNotOpenException>(() => IngestionWorld.AddItemAsync(world, batch));

        Assert.Equal(1L, await world.OwnerInDomainAsync(1, "SELECT count(*) FROM content.item_text WHERE batch_id = @id", ("id", batch)));
    }
}
