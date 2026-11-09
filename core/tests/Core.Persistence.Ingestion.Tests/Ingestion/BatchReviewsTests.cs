using System.Reflection;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Verification;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// The sample of a batch for review (issue 83, CA-1): the system draws it with the size the installation declares and nobody chooses an item,
/// the draw is in the chain, and without a size nothing is drawn.
/// </summary>
public sealed class BatchReviewsTests
{
    private const string TheDraw = "batch_sampled:administrative_act:-";

    [Fact]
    public void NothingTheRequestGivesNamesAnItem()
    {
        var requests = new[] { typeof(BatchSampling) };

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

        Assert.Equal(items, whole.Sample!.Order().ToList());
        Assert.Empty(nothing.Sample!);
    }

    [DatabaseFact]
    public async Task TheSampleIsWrittenAndReadBackAsDrawnWithItsMoment()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        _ = await ItemsAsync(world, batch, 4);

        var drawn = await DrawAsync(world, batch, 2);
        var read = await Reviews(world).ReadAsync(IngestionWorld.Domain, batch, TestContext.Current.CancellationToken);

        Assert.Equal(drawn.Sample, read!.Sample);
        Assert.Equal((BatchState.Open, true), (read.State, read.SampledAt is not null));
    }

    [DatabaseFact]
    public async Task TheSampleIsDrawnOnceAndTheDrawIsInTheChain()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        _ = await ItemsAsync(world, batch, 4);

        var first = await DrawAsync(world, batch, 2);
        var second = await DrawAsync(world, batch, 4);
        var read = await Reviews(world).ReadAsync(IngestionWorld.Domain, batch, TestContext.Current.CancellationToken);
        var verified = await new CustodyVerifier(world.DataSource, TimeProvider.System).VerifyAsync(TestContext.Current.CancellationToken);

        Assert.Equal(BatchReviewOutcome.AlreadySampled, second.Outcome);
        Assert.Equal(first.Sample, read!.Sample);
        Assert.Equal($"batch_opened:administrative_act:-,{TheDraw}", await world.ScalarAsync(IngestionWorld.Events));
        Assert.True(verified.Passed);
    }

    [DatabaseFact]
    public async Task ABatchClosedOrMissingIsNotSampledAndNothingIsRecordedForTheTry()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var closed = await IngestionWorld.OpenAsync(world);
        _ = await IngestionWorld.RejectAsync(world, closed, "invalid_rows");
        var events = await world.ScalarAsync(IngestionWorld.Events);

        var onClosed = await DrawAsync(world, closed, 1);
        var missing = await DrawAsync(world, 9_999, 1);

        Assert.Equal((BatchReviewOutcome.NotOpen, BatchReviewOutcome.NotFound, events), (onClosed.Outcome, missing.Outcome, await world.ScalarAsync(IngestionWorld.Events)));
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
