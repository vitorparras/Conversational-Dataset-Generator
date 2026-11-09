using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>A batch that is turned back as it opens is opened and rejected in one transaction (issue 75): no fall between the two leaves a batch open, and the chain holds the two events.</summary>
public sealed class BatchOpenedRejectedTests
{
    private const string Reason = "invalid_rows";
    private const string OpenedThenRejected = "batch_opened:administrative_act:-,batch_closed:batch_rejected:invalid_rows";

    [DatabaseFact]
    public async Task BatchOpenedWithAMotiveIsRejectedAtOnceWithTheMotiveAndTheTwoEvents()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batches = IngestionWorld.BatchesOf(world);

        var result = await batches.OpenAsync(new BatchOpening(IngestionWorld.Domain, IngestionWorld.Person) { RejectedFor = Reason }, TestContext.Current.CancellationToken);
        var held = await batches.ReadAsync(IngestionWorld.Domain, result.BatchId.Value, TestContext.Current.CancellationToken);

        Assert.Equal(
            (BatchOutcome.Rejected, new IngestionBatch(BatchState.Rejected, Reason, IngestionWorld.Person, TestConditions.Strictest), OpenedThenRejected),
            (result.Outcome, held, await world.ScalarAsync(IngestionWorld.Events) as string));
    }

    [DatabaseFact]
    public async Task MotiveThatIsNotAMotiveRefusesTheOpeningAndLeavesNoBatchAndNoEvent()
    {
        await using var world = await IngestionWorld.CreateAsync();

        var result = await IngestionWorld.BatchesOf(world).OpenAsync(
            new BatchOpening(IngestionWorld.Domain, IngestionWorld.Person) { RejectedFor = "Not A Motive" }, TestContext.Current.CancellationToken);

        Assert.Equal((BatchOutcome.MotiveRefused, null), (result.Outcome, await world.ScalarAsync(IngestionWorld.Events) as string));
    }
}
