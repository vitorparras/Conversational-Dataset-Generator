using Core.Host.Submissions;
using Core.Ingestion;
using Core.Persistence.Custody;
using Core.Persistence.Ingestion;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests.Submissions;

/// <summary>
/// The rejection that cleans up a batch that could not be kept (issue 75): it is made again while the chain refuses it for a collision,
/// and when the last round is refused too the batch stays open, the log says so by the batch and the domain only, and the answer is a write that failed.
/// </summary>
public sealed class SubmissionCleanUpTests
{
    private const int Domain = 7;
    private const long Batch = 42;

    [Fact]
    public async Task RejectionThatCollidesInEveryRoundLeavesTheBatchOpenAndAnswersAWriteThatFailed()
    {
        var rejections = new Rejections(Collision(), Collision(), Collision(), Rejected());
        var log = new SubmissionLog();

        var cleaned = await SubmissionStoring.CleanUpAsync(rejections.RejectAsync, true, KeptBatch(), log);

        Assert.Equal((SubmissionStoring.Keeping.WriteFailed, SubmissionStoring.RejectionRounds), (cleaned, rejections.Motives.Count));
    }

    [Fact]
    public async Task BatchLeftOpenIsLoggedAsAnErrorByTheBatchTheDomainAndTheOutcomeAndNeverByTheMaterial()
    {
        var log = new SubmissionLog();

        _ = await SubmissionStoring.CleanUpAsync(new Rejections(Collision(), Collision(), Collision()).RejectAsync, true, KeptBatch(), log);

        var line = Assert.Single(log.Lines);
        Assert.Equal((350, LogLevel.Error), (line.EventId, line.Level));
        Assert.Contains("Lote 42 do domínio 7", line.Line, StringComparison.Ordinal);
        Assert.Contains(nameof(BatchOutcome.NotRecorded), line.Line, StringComparison.Ordinal);
        Assert.DoesNotContain("ZQX", line.Line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RejectionThatCollidesTwiceAndThenTakesKeepsTheMotiveOfTheCollisionAndLogsNothing()
    {
        var rejections = new Rejections(Collision(), Collision(), Rejected());
        var log = new SubmissionLog();

        var cleaned = await SubmissionStoring.CleanUpAsync(rejections.RejectAsync, true, KeptBatch(), log);

        Assert.Equal(
            (SubmissionStoring.Keeping.Collided, 0, string.Join(',', Enumerable.Repeat(SubmissionStoring.Collision, 3))),
            (cleaned, log.Lines.Count, string.Join(',', rejections.Motives)));
    }

    [Fact]
    public async Task AcceptanceThatDidNotCollideIsRejectedAsAWriteThatFailedAndAnswersAWriteThatFailed()
    {
        var rejections = new Rejections(Rejected());

        var cleaned = await SubmissionStoring.CleanUpAsync(rejections.RejectAsync, false, KeptBatch(), new SubmissionLog());

        Assert.Equal((SubmissionStoring.Keeping.WriteFailed, SubmissionStoring.WriteFailed), (cleaned, string.Join(',', rejections.Motives)));
    }

    [Fact]
    public async Task RejectionRefusedForAnotherReasonThanACollisionIsNotRepeatedAndLeavesTheBatchOpenWithTheLog()
    {
        var refused = new BatchResult { Outcome = BatchOutcome.NotRecorded, Refusal = new CustodyResult { Outcome = CustodyOutcome.ChainBroken, Refusal = "zqx" } };
        var rejections = new Rejections(refused, Rejected());
        var log = new SubmissionLog();

        var cleaned = await SubmissionStoring.CleanUpAsync(rejections.RejectAsync, true, KeptBatch(), log);

        Assert.Equal((SubmissionStoring.Keeping.WriteFailed, 1, 1), (cleaned, rejections.Motives.Count, log.Lines.Count));
    }

    private static SubmissionStoring.Kept KeptBatch()
    {
        var instant = new DateTimeOffset(2031, 3, 4, 8, 0, 0, TimeSpan.Zero);
        var material = new ReadMaterial([new ReadUnit("ZQXSEGREDO", "ZQXREMETENTE", instant, null, [])]);
        return new SubmissionStoring.Kept(Domain, Batch, material, MaterialForm.Conversation);
    }

    private static BatchResult Collision() => new() { Outcome = BatchOutcome.NotRecorded, Refusal = new CustodyResult { Outcome = CustodyOutcome.Conflict, Refusal = "zqx" } };

    private static BatchResult Rejected() => new() { Outcome = BatchOutcome.Rejected, BatchId = Batch };

    /// <summary>The rejections, each answering the next of the results it was given, and the motive each was made for.</summary>
    private sealed class Rejections(params BatchResult[] results)
    {
        private readonly Queue<BatchResult> answers = new(results);

        public List<string> Motives { get; } = [];

        public Task<BatchResult> RejectAsync(string motive)
        {
            Motives.Add(motive);
            return Task.FromResult(answers.Dequeue());
        }
    }
}
