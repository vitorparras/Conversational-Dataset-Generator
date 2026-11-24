using Core.Persistence.Custody;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// Of two batches of the same file, both reviewed while open, the first accepted keeps the file: the acceptance of the second answers that the file was received already, leaves
/// the batch open and writes nothing to the chain (issue 101).
/// </summary>
public sealed class BatchSameFileAcceptanceTests
{
    private const string State = "SELECT state FROM content.ingestion_batch WHERE id = @id";

    private const string EventCount = "SELECT count(*) FROM public.custody_event";

    [DatabaseFact]
    public async Task SecondBatchOfTheSameFileIsNotAcceptedAndLeavesNoEvent()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var first = await IngestionWorld.OpenFileAsync(world, IngestionWorld.Domain, 'a');
        var second = await IngestionWorld.OpenFileAsync(world, IngestionWorld.Domain, 'a');
        await ReviewedBatches.ReviewCleanAsync(world.DataSource, IngestionWorld.Domain, first, IngestionWorld.Person);
        await ReviewedBatches.ReviewCleanAsync(world.DataSource, IngestionWorld.Domain, second, IngestionWorld.Person);
        Assert.Equal(BatchOutcome.Accepted, (await AcceptAsync(world, first)).Outcome);
        var events = await world.AsSuperuserAsync(EventCount);

        var repeated = await AcceptAsync(world, second);

        Assert.Equal(BatchOutcome.AlreadyReceived, repeated.Outcome);
        Assert.Equal("open", await world.AsSuperuserAsync(State, ("id", second)));
        Assert.Equal(events, await world.AsSuperuserAsync(EventCount));
    }

    private static Task<BatchResult> AcceptAsync(BatchesWorld world, long batch) =>
        IngestionWorld.BatchesOf(world).AcceptAsync(new BatchClosing(IngestionWorld.Domain, batch, CustodyAuthor.System), TestContext.Current.CancellationToken);
}
