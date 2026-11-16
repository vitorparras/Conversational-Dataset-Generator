using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Custody.Chain.Append;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// The door of the text of the sample (issue 822, migration 0059): while the batch is open, the text of an item is sealed text of the database
/// only for an item the sample drew, only after the event of the reading is in the same transaction, and in the form the item has, sanitized or raw.
/// </summary>
public sealed class BatchSampleTextTests
{
    private const string Both =
        "INSERT INTO content.item_text (domain_id, batch_id, raw_text, sanitized_text, dek_epoch, dek_wrapped) "
        + "VALUES (@domain, @batch, decode('a1', 'hex'), decode('d4', 'hex'), 1, decode('c3', 'hex')) RETURNING id";

    [DatabaseFact]
    public async Task SampleTextOfAnOpenSampledBatchIsReadOnlyAfterTheReadEvent()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var (batch, _) = await SampledAsync(world, 2);
        var item = Assert.Single(await SampleOfAsync(world, batch, 1));

        var (blockedStep, blockedText) = await ReadAsync(world, batch, item, withEvent: false);
        var (readStep, readText) = await ReadAsync(world, batch, item, withEvent: true);

        Assert.Equal((null, "raw_text_read", "raw_text_read", "a1"), (blockedText, blockedStep, readStep, readText));
    }

    [DatabaseFact]
    public async Task TextOutsideTheSampleIsRefused()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var (batch, items) = await SampledAsync(world, 3);
        var sample = await SampleOfAsync(world, batch, 1);
        var outside = items.Except(sample).First();
        var other = await IngestionWorld.OpenAsync(world);
        var elsewhere = await IngestionWorld.AddItemAsync(world, other);

        var (notDrawnStep, notDrawnText) = await ReadAsync(world, batch, outside, withEvent: true);
        var (anotherStep, anotherText) = await ReadAsync(world, batch, elsewhere, withEvent: true);

        Assert.Equal((null, null, null, null), (notDrawnStep, notDrawnText, anotherStep, anotherText));
    }

    [DatabaseFact]
    public async Task SampleTextIsTheSanitizedOneWhenItExists()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        var item = (long)(await world.InDomainAsync(IngestionWorld.Domain, Both, ("domain", IngestionWorld.Domain), ("batch", batch)))!;
        await DrawAsync(world, batch);

        var (step, text) = await ReadAsync(world, batch, item, withEvent: true);

        Assert.Equal(("sanitized_text_read", "d4"), (step, text));
    }

    [DatabaseFact]
    public async Task SampleTextIsTheRawOneWhenNoSanitizedExists()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        var item = await IngestionWorld.AddItemAsync(world, batch);
        await DrawAsync(world, batch);

        var (step, text) = await ReadAsync(world, batch, item, withEvent: true);

        Assert.Equal(("raw_text_read", "a1"), (step, text));
    }

    private static async Task DrawAsync(BatchesWorld world, long batch)
    {
        var drawn = await new BatchReviews(world.DataSource, 3).DrawAsync(new BatchSampling(IngestionWorld.Domain, batch, IngestionWorld.Person), TestContext.Current.CancellationToken);
        Assert.Equal(BatchReviewOutcome.Drawn, drawn.Outcome);
    }

    private static async Task<(long Batch, List<long> Items)> SampledAsync(BatchesWorld world, int count)
    {
        var batch = await IngestionWorld.OpenAsync(world);
        var items = new List<long>();
        for (var index = 0; index < count; index++)
        {
            items.Add(await IngestionWorld.AddItemAsync(world, batch));
        }

        var drawn = await new BatchReviews(world.DataSource, 1).DrawAsync(new BatchSampling(IngestionWorld.Domain, batch, IngestionWorld.Person), TestContext.Current.CancellationToken);
        Assert.Equal(BatchReviewOutcome.Drawn, drawn.Outcome);
        return (batch, items);
    }

    private static async Task<IReadOnlyList<long>> SampleOfAsync(BatchesWorld world, long batch, int expected)
    {
        var held = await new BatchReviews(world.DataSource, 1).ReadAsync(IngestionWorld.Domain, batch, TestContext.Current.CancellationToken);
        Assert.Equal(expected, held!.Sample!.Count);
        return held.Sample;
    }

    private static async Task<(string? Step, string? Sealed)> ReadAsync(BatchesWorld world, long batch, long item, bool withEvent)
    {
        string? step = null;
        string? sealedText = null;
        var request = new SampleReading(IngestionWorld.Domain, batch, item, IngestionWorld.Person);
        var token = TestContext.Current.CancellationToken;
        _ = await CustodyTransaction.RunAsync(
            world.DataSource,
            async Task<CustodyResult?> (scope) =>
            {
                await ReviewStatements.DeclareDomainAsync(scope, request.DomainId, token);
                step = await SampleTextStatements.StepAsync(scope, request, token);
                if (step is not null && withEvent)
                {
                    var custodyStep = step == "sanitized_text_read" ? CustodyStep.SanitizedTextRead : CustodyStep.RawTextRead;
                    var custodyItem = new CustodyItem(CustodyItemKind.ItemText, item);
                    _ = await CustodyAppend.AppendAsync(scope, CustodyEvent.Of(CustodyAuthor.Person(request.ReaderId), custodyItem, custodyStep, CustodyReason.ContentRead), token);
                }

                var held = await SampleTextStatements.SealedAsync(scope, request, token);
                sealedText = held is { } found ? Convert.ToHexStringLower(found.Sealed) : null;
                return null;
            },
            token);
        return (step, sealedText);
    }
}
