using Core.Persistence.Custody;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Content;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// A file is accepted once in a domain (issue 80, CA-2): the database refuses the acceptance of a second batch that brought the same SHA-256, which is what settles two identical files sent at the
/// same time, and the batch refused stays open for whoever closes it to reject; a batch rejected, or one of another digest, is no obstacle.
/// </summary>
public sealed class BatchAcceptedDigestTests
{
    private const string Digest = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string Other = "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";

    [DatabaseFact]
    public async Task TheSecondBatchToAcceptTheSameDigestIsRefusedStaysOpenAndIsRejectedWithTheMotiveOfTheRepeat()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await IngestionWorld.CreateAsync();
        var batches = IngestionWorld.BatchesOf(world);
        var first = await OpenAsync(batches, 'a', Digest);
        var second = await OpenAsync(batches, 'b', Digest);

        var accepted = await AcceptAsync(world, batches, first, cancellationToken);
        var refused = await AcceptAsync(world, batches, second, cancellationToken);
        var stillOpen = await batches.ReadAsync(1, second.BatchId!.Value, cancellationToken);
        var rejected = await batches.RejectAsync(Closing(second), BatchMotives.AlreadyReceived, cancellationToken);

        Assert.Equal((BatchOutcome.Accepted, BatchOutcome.AlreadyReceived, BatchState.Open, BatchOutcome.Rejected), (accepted.Outcome, refused.Outcome, stillOpen!.State, rejected.Outcome));
    }

    [DatabaseFact]
    public async Task ARejectedBatchAndABatchOfAnotherDigestDoNotStandInTheWayOfTheAcceptance()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await IngestionWorld.CreateAsync();
        var batches = IngestionWorld.BatchesOf(world);
        var turnedBack = await OpenAsync(batches, 'a', Digest);
        var same = await OpenAsync(batches, 'b', Digest);
        var different = await OpenAsync(batches, 'c', Other);

        var rejected = await batches.RejectAsync(Closing(turnedBack), "unsupported_format", cancellationToken);
        var accepted = await AcceptAsync(world, batches, same, cancellationToken);
        var another = await AcceptAsync(world, batches, different, cancellationToken);

        Assert.Equal((BatchOutcome.Rejected, BatchOutcome.Accepted, BatchOutcome.Accepted), (rejected.Outcome, accepted.Outcome, another.Outcome));
    }

    private static Task<BatchResult> OpenAsync(IngestionBatches batches, char submission, string digest) =>
        batches.OpenAsync(
            new BatchOpening(1, 1, null, new SubmissionOpening(new SubmissionReceipt(new string(submission, 32), 10, digest, null), OriginDeclaration.Unknown)),
            TestContext.Current.CancellationToken);

    private static async Task<BatchResult> AcceptAsync(ContentWorld world, IngestionBatches batches, BatchResult opened, CancellationToken cancellationToken)
    {
        await ReviewedBatches.ReviewCleanAsync(world.DataSource, 1, opened.BatchId!.Value, 1);
        return await batches.AcceptAsync(Closing(opened), cancellationToken);
    }

    private static BatchClosing Closing(BatchResult opened) => new(1, opened.BatchId!.Value, CustodyAuthor.System);
}
