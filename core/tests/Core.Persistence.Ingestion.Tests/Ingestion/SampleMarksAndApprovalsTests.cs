using Core.Persistence.Content;
using Core.Persistence.Custody;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// The mark of an item of the sample and the approval of the batch, by their own services (issue 822): a mark is the next place and nothing else, it is written for good,
/// an approval writes the review and the acceptance together and a file already accepted is not accepted twice.
/// </summary>
public sealed class SampleMarksAndApprovalsTests
{
    private const string Digest = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string Position = "SELECT reviewed_position FROM content.ingestion_batch WHERE id = @id";
    private const string State = "SELECT state || ',' || coalesce(reviewed_by::text, '-') FROM content.ingestion_batch WHERE id = @id";
    private const string Closings = "SELECT count(*) FROM public.custody_event WHERE step IN ('batch_reviewed', 'batch_closed')";

    /// <summary>
    /// Marks the sample up to a place at once, as a review that went that far before the pseudonymized text was asked of every item; the database lets a mark be only the next place.
    /// </summary>
    private const string MarkedUpTo =
        "ALTER TABLE content.ingestion_batch DISABLE TRIGGER USER; UPDATE content.ingestion_batch SET reviewed_position = @place WHERE id = @id; "
        + "ALTER TABLE content.ingestion_batch ENABLE TRIGGER USER";

    /// <summary>Takes the pseudonymized text away from the item at one place of the sample, as an item written before the pseudonymization had none.</summary>
    private const string StripAt =
        "ALTER TABLE content.item_text DISABLE TRIGGER USER; UPDATE content.item_text SET pseudonymized_text = NULL "
        + "WHERE id = (string_to_array((SELECT review_sample FROM content.ingestion_batch WHERE id = @id), ',')::bigint[])[@place]; "
        + "ALTER TABLE content.item_text ENABLE TRIGGER USER";

    [DatabaseFact]
    public async Task AMarkIsTheNextPlaceAndIsWrittenForGood()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await DrawnAsync(world, await IngestionWorld.OpenAsync(world));

        var first = await MarkAsync(world, batch, 1);
        var repeated = await MarkAsync(world, batch, 1);
        var skipped = await MarkAsync(world, batch, 3);
        var second = await MarkAsync(world, batch, 2);
        var third = await MarkAsync(world, batch, 3);
        var beyond = await MarkAsync(world, batch, 4);

        Assert.Equal(
            (BatchReviewOutcome.Marked, BatchReviewOutcome.AlreadyMarked, BatchReviewOutcome.AlreadyMarked, BatchReviewOutcome.Marked, BatchReviewOutcome.Marked, BatchReviewOutcome.AlreadyMarked, 3),
            (first.Outcome, repeated.Outcome, skipped.Outcome, second.Outcome, third.Outcome, beyond.Outcome, (int)(await world.AsSuperuserAsync(Position, ("id", batch)))!));
    }

    [DatabaseFact]
    public async Task AMarkIsRefusedForABatchThatIsMissingClosedNotSampledOrReviewed()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var closed = await IngestionWorld.OpenAsync(world);
        _ = await IngestionWorld.RejectAsync(world, closed, "invalid_rows");
        var undrawn = await IngestionWorld.OpenAsync(world);
        var reviewed = await DrawnAsync(world, await IngestionWorld.OpenAsync(world));
        _ = await new BatchReviews(world.DataSource, 3).ReviewAsync(new BatchReviewing(IngestionWorld.Domain, reviewed, IngestionWorld.Person), TestContext.Current.CancellationToken);

        var outcomes = new[]
        {
            (await MarkAsync(world, 9_999, 1)).Outcome,
            (await MarkAsync(world, closed, 1)).Outcome,
            (await MarkAsync(world, undrawn, 1)).Outcome,
            (await MarkAsync(world, reviewed, 1)).Outcome,
        };

        Assert.Equal([BatchReviewOutcome.NotFound, BatchReviewOutcome.NotOpen, BatchReviewOutcome.NotSampled, BatchReviewOutcome.AlreadyReviewed], outcomes);
    }

    [DatabaseFact]
    public async Task AnApprovalWritesTheReviewAndTheAcceptanceTogetherAndOnlyOnce()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await DrawnAsync(world, await IngestionWorld.OpenAsync(world));

        var approved = await ApproveAsync(world, batch);
        var again = await ApproveAsync(world, batch);

        Assert.Equal(
            (BatchReviewOutcome.Approved, BatchReviewOutcome.AlreadyReviewed, $"accepted,{IngestionWorld.Person}", 2L),
            (approved.Outcome, again.Outcome, (string)(await world.AsSuperuserAsync(State, ("id", batch)))!, (long)(await world.AsSuperuserAsync(Closings))!));
    }

    [DatabaseFact]
    public async Task AnApprovalOfAFileAlreadyAcceptedIsRefusedAsReceivedAndLeavesTheBatchOpen()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batches = IngestionWorld.BatchesOf(world);
        var first = await DrawnAsync(world, await OpenFileAsync(batches, 'a'));
        var second = await DrawnAsync(world, await OpenFileAsync(batches, 'b'));
        var events = (long)(await world.AsSuperuserAsync(Closings))!;

        var accepted = await ApproveAsync(world, first);
        var repeated = await ApproveAsync(world, second);

        Assert.Equal(
            (BatchReviewOutcome.Approved, BatchReviewOutcome.AlreadyReceived, "open,-", events + 2),
            (accepted.Outcome, repeated.Outcome, (string)(await world.AsSuperuserAsync(State, ("id", second)))!, (long)(await world.AsSuperuserAsync(Closings))!));
    }

    [DatabaseFact]
    public async Task AMarkOfAnItemWithoutThePseudonymizedTextIsRefusedAndWritesNothing()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await DrawnAsync(world, await IngestionWorld.OpenAsync(world), IngestionWorld.AddItemAsync);

        var refused = await MarkAsync(world, batch, 1);

        Assert.Equal((BatchReviewOutcome.NotPseudonymized, 0), (refused.Outcome, (int)(await world.AsSuperuserAsync(Position, ("id", batch)))!));
    }

    [DatabaseFact]
    public async Task AnApprovalOfASampleWithAnItemWithoutThePseudonymizedTextIsRefusedEvenWhenEveryPlaceWasMarked()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await DrawnAsync(world, await IngestionWorld.OpenAsync(world), IngestionWorld.AddItemAsync);
        _ = await world.AsSuperuserAsync(MarkedUpTo, ("id", batch), ("place", 3));
        var events = (long)(await world.AsSuperuserAsync(Closings))!;

        var refused = await ApproveAsync(world, batch);

        Assert.Equal(
            (BatchReviewOutcome.NotPseudonymized, "open,-", events),
            (refused.Outcome, (string)(await world.AsSuperuserAsync(State, ("id", batch)))!, (long)(await world.AsSuperuserAsync(Closings))!));
    }

    [DatabaseFact]
    public async Task AFindingStillRejectsASampleWithAnItemWithoutThePseudonymizedText()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await DrawnAsync(world, await IngestionWorld.OpenAsync(world), IngestionWorld.AddItemAsync);

        var rejected = await new BatchReviews(world.DataSource, 3).ReviewAsync(
            new BatchReviewing(IngestionWorld.Domain, batch, IngestionWorld.Person, "nome", 1), TestContext.Current.CancellationToken);

        Assert.Equal((BatchReviewOutcome.Rejected, $"rejected,{IngestionWorld.Person}"), (rejected.Outcome, (string)(await world.AsSuperuserAsync(State, ("id", batch)))!));
    }

    [DatabaseTheory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task AnApprovalIsRefusedWhenOneItemOfTheSampleAloneLacksThePseudonymizedTextWhereverItIs(int place)
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await DrawnAsync(world, await IngestionWorld.OpenAsync(world));
        _ = await world.AsSuperuserAsync(StripAt, ("id", batch), ("place", place));
        _ = await world.AsSuperuserAsync(MarkedUpTo, ("id", batch), ("place", 3));

        var refused = await ApproveAsync(world, batch);

        Assert.Equal((BatchReviewOutcome.NotPseudonymized, "open,-"), (refused.Outcome, (string)(await world.AsSuperuserAsync(State, ("id", batch)))!));
    }

    [DatabaseFact]
    public async Task AMarkIsRefusedWhenAnItemBeforeItLacksThePseudonymizedTextEvenIfThatOneWasMarkedBefore()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await DrawnAsync(world, await IngestionWorld.OpenAsync(world));
        _ = await world.AsSuperuserAsync(StripAt, ("id", batch), ("place", 1));
        _ = await world.AsSuperuserAsync(MarkedUpTo, ("id", batch), ("place", 1));

        var refused = await MarkAsync(world, batch, 2);

        Assert.Equal((BatchReviewOutcome.NotPseudonymized, 1), (refused.Outcome, (int)(await world.AsSuperuserAsync(Position, ("id", batch)))!));
    }

    [DatabaseFact]
    public async Task TheStatementOfTheMarkItselfMarksNoPlaceUpToAnItemWithoutThePseudonymizedText()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await DrawnAsync(world, await IngestionWorld.OpenAsync(world));
        _ = await world.AsSuperuserAsync(StripAt, ("id", batch), ("place", 2));
        var mark = $"WITH changed AS ({ContentQueries.CloseBatchPlace} RETURNING id) SELECT count(*) FROM changed";

        var first = await world.InDomainAsync(IngestionWorld.Domain, mark, ("id", batch), ("position", 1));
        var second = await world.InDomainAsync(IngestionWorld.Domain, mark, ("id", batch), ("position", 2));

        Assert.Equal((1L, 0L, 1), ((long)first!, (long)second!, (int)(await world.AsSuperuserAsync(Position, ("id", batch)))!));
    }

    [Fact]
    public void ARefusalByCollisionIsOneThatWroteNothingAndMayBeAskedAgain()
    {
        var conflict = new CustodyResult { Outcome = CustodyOutcome.Conflict, Refusal = "same link" };
        var other = new CustodyResult { Outcome = CustodyOutcome.Appended, Sequence = 1 };

        Assert.Equal(
            (true, false, false, false),
            (
                new BatchReviewResult { Outcome = BatchReviewOutcome.NotRecorded, Refusal = conflict }.Collided,
                new BatchReviewResult { Outcome = BatchReviewOutcome.NotRecorded, Refusal = other }.Collided,
                new BatchReviewResult { Outcome = BatchReviewOutcome.NotRecorded }.Collided,
                new BatchReviewResult { Outcome = BatchReviewOutcome.Rejected, Refusal = conflict }.Collided));
    }

    private static Task<BatchReviewResult> MarkAsync(BatchesWorld world, long batch, int place) =>
        new SampleMarks(world.DataSource).MarkAsync(new BatchMarking(IngestionWorld.Domain, batch, place), TestContext.Current.CancellationToken);

    private static Task<BatchReviewResult> ApproveAsync(BatchesWorld world, long batch) =>
        new BatchApprovals(world.DataSource).ApproveAsync(new BatchApproval(IngestionWorld.Domain, batch, IngestionWorld.Person), TestContext.Current.CancellationToken);

    private static async Task<long> OpenFileAsync(IngestionBatches batches, char submission)
    {
        var opened = await batches.OpenAsync(
            new BatchOpening(IngestionWorld.Domain, IngestionWorld.Person, null, new SubmissionOpening(new SubmissionReceipt(new string(submission, 32), 10, Digest, null), OriginDeclaration.Unknown)),
            TestContext.Current.CancellationToken);
        Assert.Equal(BatchOutcome.Opened, opened.Outcome);
        return opened.BatchId!.Value;
    }

    private static Task<long> DrawnAsync(BatchesWorld world, long batch) => DrawnAsync(world, batch, IngestionWorld.AddPseudonymizedItemAsync);

    private static async Task<long> DrawnAsync(BatchesWorld world, long batch, Func<BatchesWorld, long, Task<long>> add)
    {
        for (var item = 0; item < 3; item++)
        {
            _ = await add(world, batch);
        }

        var drawn = await new BatchReviews(world.DataSource, 3).DrawAsync(new BatchSampling(IngestionWorld.Domain, batch, IngestionWorld.Person), TestContext.Current.CancellationToken);
        Assert.Equal(BatchReviewOutcome.Drawn, drawn.Outcome);
        return batch;
    }
}
