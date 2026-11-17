using System.Diagnostics;
using Core.Ingestion.Contracts;
using Core.Persistence.Custody;
using Core.Persistence.Ingestion;
using Npgsql;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// What the review of the sample refuses by the state the batch is in, the sample it draws and the answer it gives when the chain did not take the event, none of it over a database (issue 822),
/// and what the marks, the approvals and the readings refuse before they touch one.
/// </summary>
public sealed class BatchReviewRulesTests
{
    private static readonly IReadOnlyList<long> Sample = [4, 7];
    private static readonly DateTimeOffset Moment = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ABatchThatDoesNotExistIsNotFoundWhateverIsAsked() =>
        Assert.Equal(
            (BatchReviewOutcome.NotFound, BatchReviewOutcome.NotFound),
            (BatchReviews.Refusal(null, drawing: true)!.Value, BatchReviews.Refusal(null, drawing: false)!.Value));

    [Fact]
    public void DrawingIsRefusedForABatchThatIsNotOpenOrAlreadySampledAndAllowedOtherwise() =>
        Assert.Equal(
            [BatchReviewOutcome.NotOpen, BatchReviewOutcome.NotOpen, BatchReviewOutcome.AlreadySampled, null],
            [
                BatchReviews.Refusal(Held(BatchState.Accepted, sample: null), drawing: true),
                BatchReviews.Refusal(Held(BatchState.Rejected, sample: Sample), drawing: true),
                BatchReviews.Refusal(Held(BatchState.Open, sample: Sample), drawing: true),
                BatchReviews.Refusal(Held(BatchState.Open, sample: null), drawing: true),
            ]);

    [Fact]
    public void AReviewIsRefusedForABatchWithoutSampleOrAlreadyReviewedAndAllowedWhileTheSampleWaits() =>
        Assert.Equal(
            [BatchReviewOutcome.NotSampled, BatchReviewOutcome.AlreadyReviewed, null],
            [
                BatchReviews.Refusal(Held(BatchState.Open, sample: null), drawing: false),
                BatchReviews.Refusal(Held(BatchState.Open, sample: Sample, reviewer: 3), drawing: false),
                BatchReviews.Refusal(Held(BatchState.Open, sample: Sample), drawing: false),
            ]);

    [Fact]
    public void ABatchThatClosedWithoutAReviewIsNotOpenAndOneThatClosedAfterTheReviewIsAlreadyReviewed() =>
        Assert.Equal(
            [BatchReviewOutcome.NotOpen, BatchReviewOutcome.AlreadyReviewed, BatchReviewOutcome.AlreadyReviewed],
            [
                BatchReviews.Refusal(Held(BatchState.Rejected, sample: null), drawing: false),
                BatchReviews.Refusal(Held(BatchState.Accepted, sample: Sample, reviewer: 3), drawing: false),
                BatchReviews.Refusal(Held(BatchState.Rejected, sample: Sample, reviewer: 3), drawing: false),
            ]);

    [Fact]
    public void ARefusalThatNoRuleDeclaresIsNeitherAnsweredNorGuessed() =>
        Assert.Throws<UnreachableException>(() => BatchReviews.Outcome((SampleRefusal)99));

    [Fact]
    public void ADrawnSampleHasTheSizeAskedIsInAscendingOrderHasNoRepeatedNumberAndIsNotAlwaysTheSame()
    {
        var numbers = Enumerable.Range(1, 40).Select(number => (long)number).ToArray();
        var draws = Enumerable.Range(0, 12).Select(_ => BatchReviews.Drawn(numbers, 5)).ToArray();

        Assert.All(
            draws,
            draw => Assert.Equal((5, true, true), (draw.Length, draw.SequenceEqual(draw.Order()), draw.Distinct().Count() == 5 && Array.TrueForAll(draw, number => number is >= 1 and <= 40))));
        Assert.True(draws.Select(draw => string.Join(',', draw)).Distinct(StringComparer.Ordinal).Count() > 1, "Twelve draws of five among forty came out the same.");
        Assert.Contains(draws, draw => !draw.SequenceEqual(numbers.Take(5)));
        Assert.Equal(numbers, BatchReviews.Drawn(numbers, 40));
    }

    [Fact]
    public void ADrawnSampleNeverTakesMoreThanThereIsAndNeverChangesTheListGiven()
    {
        var numbers = new long[] { 9, 3, 6 };

        var drawn = BatchReviews.Drawn(numbers, 10);

        Assert.Equal([3L, 6L, 9L], drawn);
        Assert.Equal([9L, 3L, 6L], numbers);
    }

    [Fact]
    public void AnEventTheChainTookIsAnsweredWithItsSequenceAndOneItDidNotTakeIsNotRecorded()
    {
        var taken = BatchReviews.Answered(new CustodyResult { Outcome = CustodyOutcome.Appended, Sequence = 12 }, BatchReviewOutcome.Approved, Sample);
        var refused = new CustodyResult { Outcome = CustodyOutcome.Conflict, Refusal = "same link" };
        var left = BatchReviews.Answered(refused, BatchReviewOutcome.Approved, Sample);
        var missing = BatchReviews.Answered(null, BatchReviewOutcome.Approved, Sample);

        Assert.Equal((BatchReviewOutcome.Approved, 12, Sample, null), (taken.Outcome, taken.Sequence, taken.Sample, taken.Refusal));
        Assert.Equal((BatchReviewOutcome.NotRecorded, null, refused), (left.Outcome, left.Sequence, left.Refusal));
        Assert.Equal((BatchReviewOutcome.NotRecorded, null), (missing.Outcome, missing.Refusal));
    }

    [Fact]
    public async Task AMarkNamedByAnIdentifierThatIsNotPositiveIsRefusedByThatIdentifier()
    {
        await using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=u;Password=p;Database=d");
        var marks = new SampleMarks(dataSource);

        var refused = new List<string?> { (await Assert.ThrowsAsync<ArgumentNullException>(() => marks.MarkAsync(null!, TestContext.Current.CancellationToken))).ParamName };
        foreach (var request in new[] { new BatchMarking(0, 1, 1), new BatchMarking(1, 0, 1), new BatchMarking(1, 1, 0) })
        {
            refused.Add((await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => marks.MarkAsync(request, TestContext.Current.CancellationToken))).ParamName);
        }

        Assert.Equal(["request", "request.DomainId", "request.BatchId", "request.Place"], refused);
    }

    [Fact]
    public async Task AnApprovalNamedByAnIdentifierThatIsNotPositiveIsRefusedByThatIdentifier()
    {
        await using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=u;Password=p;Database=d");
        var approvals = new BatchApprovals(dataSource);

        var refused = new List<string?> { (await Assert.ThrowsAsync<ArgumentNullException>(() => approvals.ApproveAsync(null!, TestContext.Current.CancellationToken))).ParamName };
        foreach (var request in new[] { new BatchApproval(0, 1, 1), new BatchApproval(1, 0, 1), new BatchApproval(1, 1, 0) })
        {
            refused.Add((await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => approvals.ApproveAsync(request, TestContext.Current.CancellationToken))).ParamName);
        }

        Assert.Equal(["request", "request.DomainId", "request.BatchId", "request.ReviewerId"], refused);
    }

    private static BatchReview Held(BatchState state, IReadOnlyList<long>? sample, long? reviewer = null) =>
        new(state, sample, sample is null ? null : Moment, reviewer, reviewer is null ? null : Moment, null);
}
