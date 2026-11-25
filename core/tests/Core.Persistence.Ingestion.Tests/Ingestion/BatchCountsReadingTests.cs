using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>The counts of a batch are read over a source of connections, and a batch with no item yet has no version and no item counted (issue 101).</summary>
public sealed class BatchCountsReadingTests
{
    [Fact]
    public void CountsAreNotReadWithoutASourceOfConnections()
    {
        var refused = Assert.Throws<ArgumentNullException>(() => new BatchCounts(null!));

        Assert.Equal("dataSource", refused.ParamName);
    }

    [DatabaseFact]
    public async Task BatchWithNoItemHasNoVersionNoItemsAndNoMarkers()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);

        var counts = await new BatchCounts(world.DataSource).ReadAsync(IngestionWorld.Domain, batch, TestContext.Current.CancellationToken);

        Assert.Null(counts.SanitizationVersion);
        Assert.Equal((0L, 0L), (counts.Items, counts.SanitizedItems));
        Assert.Empty(counts.Markers);
        Assert.Empty(counts.Steps);
    }
}
