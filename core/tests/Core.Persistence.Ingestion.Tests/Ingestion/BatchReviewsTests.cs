using System.Reflection;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Verification;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// The review of a batch by sample (issue 83): the system draws the sample with the size the installation declares and nobody chooses an item, a finding rejects
/// the whole batch with its material, both are in the chain whoever reviewed and whatever became of the batch,
/// and no batch is accepted without a sample that was reviewed and found clean.
/// </summary>
public sealed class BatchReviewsTests
{
    private const string TheDraw = "batch_sampled:administrative_act:-";
    private const string Finding = "contact_not_treated";
    private const string TheFinding = "batch_reviewed:review_found:-,batch_closed:batch_rejected:" + BatchReviews.RejectionMotive;

    [Fact]
    public void NothingTheReviewerGivesNamesAnItem()
    {
        var requests = new[] { typeof(BatchSampling), typeof(BatchReviewing) };

        var types = requests.SelectMany(request => request.GetProperties(BindingFlags.Public | BindingFlags.Instance)).Select(property => property.PropertyType).Distinct().ToList();

        Assert.DoesNotContain(types, type => type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)));
    }

    [DatabaseFact]
    public async Task TheSampleHasTheSizeAskedAndHoldsOnlyItemsOfTheBatchOnceEach()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        var items = await ItemsAsync(world, batch, 6);

        var drawn = await DrawAsync(world, batch, 3);

        Assert.Equal((BatchReviewOutcome.Drawn, 3, 3), (drawn.Outcome, drawn.Sample!.Count, drawn.Sample.Distinct().Count()));
        Assert.All(drawn.Sample, item => Assert.Contains(item, items));
    }

    [DatabaseFact]
    public async Task ABatchSmallerThanTheSizeIsSampledWholeAndAnEmptyOneIsSampledEmpty()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var small = await IngestionWorld.OpenAsync(world);
        var items = await ItemsAsync(world, small, 2);
        var empty = await IngestionWorld.OpenAsync(world);

        var whole = await DrawAsync(world, small, 5);
        var nothing = await DrawAsync(world, empty, 5);

        Assert.Equal(items, [.. whole.Sample!.Order()]);
        Assert.Empty(nothing.Sample!);
    }

    [DatabaseFact]
    public async Task TheSampleIsWrittenBeforeTheReviewAndReadBackAsDrawn()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        _ = await ItemsAsync(world, batch, 4);

        var drawn = await DrawAsync(world, batch, 2);
        var read = await Reviews(world).ReadAsync(IngestionWorld.Domain, batch, TestContext.Current.CancellationToken);

        Assert.Equal(drawn.Sample, read!.Sample);
        Assert.Equal((null, true), (read.ReviewerId, read.SampledAt is not null));
    }

    [DatabaseFact]
    public async Task TheSampleIsDrawnOnceAndAReviewNeedsOne()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        _ = await ItemsAsync(world, batch, 4);
        var early = await ReviewAsync(world, batch, null);

        var first = await DrawAsync(world, batch, 2);
        var second = await DrawAsync(world, batch, 4);
        var read = await Reviews(world).ReadAsync(IngestionWorld.Domain, batch, TestContext.Current.CancellationToken);

        Assert.Equal((BatchReviewOutcome.NotSampled, BatchReviewOutcome.AlreadySampled), (early.Outcome, second.Outcome));
        Assert.Equal(first.Sample, read!.Sample);
    }

    [DatabaseFact]
    public async Task ACleanReviewIsInTheChainAndReadsBackWithTheSampleTheReviewerAndTheMoment()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        _ = await ItemsAsync(world, batch, 3);
        var drawn = await DrawAsync(world, batch, 2);

        var reviewed = await ReviewAsync(world, batch, null);
        var accepted = await IngestionWorld.AcceptAsync(world, batch);
        var read = await Reviews(world).ReadAsync(IngestionWorld.Domain, batch, TestContext.Current.CancellationToken);

        Assert.Equal((BatchReviewOutcome.Reviewed, BatchOutcome.Accepted), (reviewed.Outcome, accepted.Outcome));
        Assert.Equal(drawn.Sample, read!.Sample);
        Assert.Equal((BatchState.Accepted, IngestionWorld.Person, null, true), (read.State, read.ReviewerId, read.Finding, read.ReviewedAt is not null));
        Assert.Equal($"batch_opened:administrative_act:-,{TheDraw},batch_reviewed:review_clean:-,batch_closed:batch_accepted:-", await world.ScalarAsync(IngestionWorld.Events));
    }

    [DatabaseFact]
    public async Task AFindingRejectsTheWholeBatchAndNoItemOfItIsReachable()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        var items = await ItemsAsync(world, batch, 4);
        _ = await IngestionWorld.AddMapAsync(world, batch, items[0]);
        _ = await DrawAsync(world, batch, 1);

        var found = await ReviewAsync(world, batch, Finding);
        var held = await IngestionWorld.BatchesOf(world).ReadAsync(IngestionWorld.Domain, batch, TestContext.Current.CancellationToken);

        Assert.Equal(BatchReviewOutcome.Rejected, found.Outcome);
        Assert.Equal(new IngestionBatch(BatchState.Rejected, BatchReviews.RejectionMotive, IngestionWorld.Person, TestConditions.Strictest), held);
        Assert.Equal("0,0", await world.InDomainAsync(IngestionWorld.Domain, IngestionWorld.Reachable) as string);
    }

    [DatabaseFact]
    public async Task TheRejectionByAFindingKeepsWhoReviewedWhatWasFoundAndWhenEvenThoughTheBatchNeverEnters()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        _ = await ItemsAsync(world, batch, 2);
        var drawn = await DrawAsync(world, batch, 2);

        _ = await ReviewAsync(world, batch, Finding);
        var read = await Reviews(world).ReadAsync(IngestionWorld.Domain, batch, TestContext.Current.CancellationToken);
        var events = await world.ScalarAsync(IngestionWorld.Events);
        var verified = await new CustodyVerifier(world.DataSource, TimeProvider.System).VerifyAsync(TestContext.Current.CancellationToken);

        Assert.Equal(drawn.Sample, read!.Sample);
        Assert.Equal((BatchState.Rejected, IngestionWorld.Person, Finding, true), (read.State, read.ReviewerId, read.Finding, read.ReviewedAt is not null));
        Assert.Equal($"batch_opened:administrative_act:-,{TheDraw},{TheFinding}", events);
        Assert.True(verified.Passed);
    }

    [DatabaseFact]
    public async Task AFindingThatIsNotAWordOfTheClosedFormIsRefusedAndNothingChanges()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        _ = await ItemsAsync(world, batch, 2);
        _ = await DrawAsync(world, batch, 2);

        var refused = await ReviewAsync(world, batch, "Nome de Fulano zqx");

        var held = await Reviews(world).ReadAsync(IngestionWorld.Domain, batch, TestContext.Current.CancellationToken);

        Assert.Equal((BatchReviewOutcome.FindingRefused, BatchState.Open), (refused.Outcome, held!.State));
    }

    [DatabaseFact]
    public async Task ABatchIsReviewedOnceAndAClosedOrMissingOneIsNotReviewedAtAll()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        var closed = await IngestionWorld.OpenAsync(world);
        _ = await DrawAsync(world, batch, 1);
        _ = await ReviewAsync(world, batch, null);
        _ = await IngestionWorld.RejectAsync(world, closed, "invalid_rows");

        var again = await ReviewAsync(world, batch, null);
        var onClosed = await DrawAsync(world, closed, 1);
        var missing = await DrawAsync(world, 9_999, 1);

        Assert.Equal(
            (BatchReviewOutcome.AlreadyReviewed, BatchReviewOutcome.NotOpen, BatchReviewOutcome.NotFound),
            (again.Outcome, onClosed.Outcome, missing.Outcome));
    }

    [DatabaseFact]
    public async Task NoPathAcceptsABatchWhoseSampleWasNotReviewedAndNothingIsRecordedForTheTry()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var untouched = await IngestionWorld.OpenAsync(world);
        var sampled = await IngestionWorld.OpenAsync(world);
        _ = await DrawAsync(world, sampled, 1);
        var events = await world.ScalarAsync(IngestionWorld.Events);
        var batches = IngestionWorld.BatchesOf(world);

        var withoutSample = await batches.AcceptAsync(new BatchClosing(IngestionWorld.Domain, untouched, CustodyAuthor.System), TestContext.Current.CancellationToken);
        var withoutReview = await batches.AcceptAsync(new BatchClosing(IngestionWorld.Domain, sampled, CustodyAuthor.System), TestContext.Current.CancellationToken);

        Assert.Equal((BatchOutcome.ReviewMissing, BatchOutcome.ReviewMissing, events), (withoutSample.Outcome, withoutReview.Outcome, await world.ScalarAsync(IngestionWorld.Events)));
    }

    [DatabaseFact]
    public async Task WithoutASizeFromTheInstallationNothingIsDrawnAndNothingIsRecorded()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        _ = await ItemsAsync(world, batch, 2);
        var events = await world.ScalarAsync(IngestionWorld.Events);

        var refused = await new BatchReviews(world.DataSource, null).DrawAsync(new BatchSampling(IngestionWorld.Domain, batch, IngestionWorld.Person), TestContext.Current.CancellationToken);
        var read = await Reviews(world).ReadAsync(IngestionWorld.Domain, batch, TestContext.Current.CancellationToken);

        Assert.Equal(BatchReviewOutcome.SizeNotDeclared, refused.Outcome);
        Assert.Null(read!.Sample);
        Assert.Equal(events, await world.ScalarAsync(IngestionWorld.Events));
    }

    [DatabaseTheory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ASizeThatIsNotFromOneOnIsADefectOfWhoBuildsTheService(int size)
    {
        await using var world = await IngestionWorld.CreateAsync();

        var defect = Assert.Throws<ArgumentOutOfRangeException>(() => new BatchReviews(world.DataSource, size));

        Assert.Equal("sampleSize", defect.ParamName);
    }

    private static BatchReviews Reviews(BatchesWorld world) => new(world.DataSource, 3);

    private static Task<BatchReviewResult> DrawAsync(BatchesWorld world, long batch, int size) =>
        new BatchReviews(world.DataSource, size).DrawAsync(new BatchSampling(IngestionWorld.Domain, batch, IngestionWorld.Person), TestContext.Current.CancellationToken);

    private static Task<BatchReviewResult> ReviewAsync(BatchesWorld world, long batch, string? finding) =>
        Reviews(world).ReviewAsync(new BatchReviewing(IngestionWorld.Domain, batch, IngestionWorld.Person, finding), TestContext.Current.CancellationToken);

    private static async Task<List<long>> ItemsAsync(BatchesWorld world, long batch, int count)
    {
        var items = new List<long>();
        for (var index = 0; index < count; index++)
        {
            items.Add(await IngestionWorld.AddItemAsync(world, batch));
        }

        return items;
    }
}
