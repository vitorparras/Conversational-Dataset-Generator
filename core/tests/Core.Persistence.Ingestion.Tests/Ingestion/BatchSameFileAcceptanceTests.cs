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

    private static readonly string Digest = new('d', 64);

    [DatabaseFact]
    public async Task SecondBatchOfTheSameFileIsNotAcceptedAndLeavesNoEvent()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var first = await OpenFileAsync(world, 'a');
        var second = await OpenFileAsync(world, 'b');
        await ReviewedBatches.ReviewCleanAsync(world.DataSource, IngestionWorld.Domain, first, IngestionWorld.Person);
        await ReviewedBatches.ReviewCleanAsync(world.DataSource, IngestionWorld.Domain, second, IngestionWorld.Person);
        Assert.Equal(BatchOutcome.Accepted, (await AcceptAsync(world, first)).Outcome);
        var events = await world.AsSuperuserAsync(EventCount);

        var repeated = await AcceptAsync(world, second);

        Assert.Equal(BatchOutcome.AlreadyReceived, repeated.Outcome);
        Assert.Equal("open", await world.AsSuperuserAsync(State, ("id", second)));
        Assert.Equal(events, await world.AsSuperuserAsync(EventCount));
    }

    /// <summary>Each submission is its own, and both carry the same file.</summary>
    private static async Task<long> OpenFileAsync(BatchesWorld world, char submission)
    {
        var receipt = new SubmissionReceipt(new string(submission, 32), 10, Digest, null);
        var opened = await IngestionWorld.BatchesOf(world).OpenAsync(
            new BatchOpening(IngestionWorld.Domain, IngestionWorld.Person, null, new SubmissionOpening(receipt, OriginDeclaration.Unknown)),
            TestContext.Current.CancellationToken);
        Assert.Equal(BatchOutcome.Opened, opened.Outcome);
        return opened.BatchId!.Value;
    }

    private static Task<BatchResult> AcceptAsync(BatchesWorld world, long batch) =>
        IngestionWorld.BatchesOf(world).AcceptAsync(new BatchClosing(IngestionWorld.Domain, batch, CustodyAuthor.System), TestContext.Current.CancellationToken);
}
