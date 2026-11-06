using Core.Persistence.Content.Writing;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>The writer of content adds only to a batch that is open, of its own domain, and says so with its own exception (issue 74, criterion 2).</summary>
public sealed class IngestionContentWritingTests
{
    [DatabaseFact]
    public async Task ContentIsNotAddedToABatchThatClosed()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var accepted = await IngestionWorld.OpenAsync(world);
        var item = await IngestionWorld.AddItemAsync(world, accepted);
        _ = await IngestionWorld.AcceptAsync(world, accepted);
        var rejected = await IngestionWorld.OpenAsync(world);
        _ = await IngestionWorld.RejectAsync(world, rejected, "invalid_rows");

        _ = await Assert.ThrowsAsync<BatchNotOpenException>(() => IngestionWorld.AddItemAsync(world, accepted));
        _ = await Assert.ThrowsAsync<BatchNotOpenException>(() => IngestionWorld.AddItemAsync(world, rejected));
        _ = await Assert.ThrowsAsync<BatchNotOpenException>(() => IngestionWorld.AddMapAsync(world, accepted, item));
    }

    [DatabaseFact]
    public async Task ContentIsNotAddedToABatchOfAnotherDomainOrOneThatDoesNotExist()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        var item = new NewItemText("texto bruto sintetico zqx");

        _ = await Assert.ThrowsAsync<BatchNotOpenException>(() => world.RealWriter.AddItemTextAsync(2, batch, item, TestContext.Current.CancellationToken));
        _ = await Assert.ThrowsAsync<BatchNotOpenException>(() => world.RealWriter.AddItemTextAsync(IngestionWorld.Domain, 9_999, item, TestContext.Current.CancellationToken));
    }
}
