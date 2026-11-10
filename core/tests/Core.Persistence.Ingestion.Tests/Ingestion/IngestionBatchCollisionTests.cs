using Core.Persistence.Ingestion;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// Writers of the chain that reach the same link together (issue 75): the batches repeat the opening, the acceptance and the rejection that lose,
/// so that a collision costs the caller nothing, and the batches that win are exactly the ones in the database with the chain in order.
/// </summary>
public sealed class IngestionBatchCollisionTests
{
    private const int Simultaneous = 8;
    private const string BatchesOfTheDomain = "SELECT count(*)::text FROM content.ingestion_batch";
    private const string ChainInOrder = "SELECT (count(*) = max(sequence) AND min(sequence) = 1)::text FROM public.custody_event";
    private const string RejectedMotive = "SELECT motive FROM content.ingestion_batch WHERE id = @batch";

    [DatabaseFact]
    public async Task OpeningThatMeetsAWriterNotYetCommittedIsRepeatedAndOpensTheBatch()
    {
        await using var world = await IngestionWorld.CreateAsync();

        var opened = await ChainContention.WhilePendingAsync(
            world,
            () => IngestionWorld.BatchesOf(world).OpenAsync(new BatchOpening(IngestionWorld.Domain, IngestionWorld.Person, null), TestContext.Current.CancellationToken));

        Assert.Equal((BatchOutcome.Opened, false, "1"), (opened.Outcome, opened.Collided, await world.InDomainAsync(IngestionWorld.Domain, BatchesOfTheDomain) as string));
    }

    [DatabaseFact]
    public async Task AcceptanceThatMeetsAWriterNotYetCommittedIsRepeatedAndAcceptsTheBatch()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);

        var accepted = await ChainContention.WhilePendingAsync(world, () => IngestionWorld.AcceptAsync(world, batch));

        Assert.Equal((BatchOutcome.Accepted, false), (accepted.Outcome, accepted.Collided));
    }

    [DatabaseFact]
    public async Task RejectionThatMeetsAWriterNotYetCommittedIsRepeatedAndRejectsTheBatchForTheMotiveGiven()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);

        var rejected = await ChainContention.WhilePendingAsync(world, () => IngestionWorld.RejectAsync(world, batch, "concurrent_submission"));

        Assert.Equal((BatchOutcome.Rejected, false, "concurrent_submission"), (rejected.Outcome, rejected.Collided, await world.InDomainAsync(IngestionWorld.Domain, RejectedMotive, ("batch", batch)) as string));
    }

    [DatabaseFact]
    public async Task EightBatchesOpenedTogetherAreEachOpenedOrToldToTryAgainAndTheChainStaysInOrder()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batches = IngestionWorld.BatchesOf(world);

        var results = await Task.WhenAll(Enumerable.Range(0, Simultaneous).Select(_ =>
            Task.Run(() => batches.OpenAsync(new BatchOpening(IngestionWorld.Domain, IngestionWorld.Person, null), TestContext.Current.CancellationToken), TestContext.Current.CancellationToken)));

        var opened = results.Count(result => result.Outcome == BatchOutcome.Opened);
        var told = results.Count(result => result.Collided && result.BatchId is null);
        Assert.Equal((Simultaneous, opened.ToString(System.Globalization.CultureInfo.InvariantCulture), "true"), (opened + told, await world.InDomainAsync(IngestionWorld.Domain, BatchesOfTheDomain) as string, await world.ScalarAsync(ChainInOrder) as string));
    }
}
