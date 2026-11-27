using Core.Persistence.Content.Reading;
using Core.Persistence.Content.Writing;
using Core.Persistence.Custody.Verification;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Content;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// The reading of the text of an item of the sample by its own service (issues 822 and 939): the text is the pseudonymized one and never the sanitized nor the raw, the event of the reading
/// is written once and before the text is opened, the map is never read, an item without the pseudonymized text shows nothing and records nothing, an item outside the sample is not found
/// and records nothing, a reading that meets another writer on the chain is repeated, and a reading goes on, recorded, while the chain is broken.
/// </summary>
public sealed class SampleTextReaderTests
{
    private const string Reads =
        "SELECT string_agg(step, ',' ORDER BY sequence) FROM public.custody_event WHERE step IN ('raw_text_read', 'sanitized_text_read', 'pseudonymized_text_read', 'reversal_entries_read')";

    private const string Pseudonymized = "texto pseudonimizado PESSOA_zqx";

    private static readonly string[] Labels = ["texto-bruto/1/v1", "mapa-reversao/1/v1", "texto-bruto/2/v1", "mapa-reversao/2/v1"];

    [DatabaseFact]
    public async Task ThePseudonymizedTextIsReadAndTheReadingIsRecordedOnceWithoutTheMap()
    {
        await using var world = await IngestionWorld.CreateAsync();
        using var custody = SyntheticCustody.Holding(Labels);
        var (batch, item) = await SampledAsync(world, custody);

        var read = await ReadAsync(world, custody, batch, item);

        Assert.Equal(
            (ContentReadOutcome.Read, Pseudonymized, item, "pseudonymized_text_read"),
            (read.Outcome, read.Text, read.RowId, await world.AsSuperuserAsync(Reads) as string));
    }

    [DatabaseFact]
    public async Task AnItemWithoutThePseudonymizedTextShowsNoTextAndNothingIsRecorded()
    {
        await using var world = await IngestionWorld.CreateAsync();
        using var custody = SyntheticCustody.Holding(Labels);
        var batch = await IngestionWorld.OpenAsync(world);
        var item = await new ContentWriter(world.DataSource, custody.Content).AddItemTextAsync(
            IngestionWorld.Domain, batch, new NewItemText("texto bruto zqx") { Sanitization = ItemTexts.Sanitization("texto sanitizado zqx") }, TestContext.Current.CancellationToken);
        await DrawAsync(world, batch);

        var read = await ReadAsync(world, custody, batch, item);

        Assert.Equal((ContentReadOutcome.Empty, null, item, null), (read.Outcome, read.Text, read.RowId, await world.AsSuperuserAsync(Reads) as string));
    }

    [DatabaseFact]
    public async Task EachReadingOfTheSameItemIsOneEventOfItsOwn()
    {
        await using var world = await IngestionWorld.CreateAsync();
        using var custody = SyntheticCustody.Holding(Labels);
        var (batch, item) = await SampledAsync(world, custody);

        _ = await ReadAsync(world, custody, batch, item);
        _ = await ReadAsync(world, custody, batch, item);

        Assert.Equal("pseudonymized_text_read,pseudonymized_text_read", await world.AsSuperuserAsync(Reads) as string);
    }

    [DatabaseFact]
    public async Task AnItemOutsideTheSampleIsNotFoundAndNothingIsRecorded()
    {
        await using var world = await IngestionWorld.CreateAsync();
        using var custody = SyntheticCustody.Holding(Labels);
        var (batch, _) = await SampledAsync(world, custody);
        var other = await IngestionWorld.OpenAsync(world);
        var elsewhere = await new ContentWriter(world.DataSource, custody.Content).AddItemTextAsync(
            IngestionWorld.Domain, other, new NewItemText("texto de outro lote zqx"), TestContext.Current.CancellationToken);

        var read = await ReadAsync(world, custody, batch, elsewhere);

        Assert.Equal((ContentReadOutcome.NotFound, null, elsewhere, null), (read.Outcome, read.Text, read.RowId, await world.AsSuperuserAsync(Reads) as string));
    }

    [DatabaseFact]
    public async Task AReadingThatMeetsAWriterNotYetCommittedIsRepeatedAndReadsTheText()
    {
        await using var world = await IngestionWorld.CreateAsync();
        using var custody = SyntheticCustody.Holding(Labels);
        var (batch, item) = await SampledAsync(world, custody);

        var read = await ChainContention.WhilePendingAsync(world, () => ReadAsync(world, custody, batch, item));

        Assert.Equal((ContentReadOutcome.Read, Pseudonymized, "pseudonymized_text_read"), (read.Outcome, read.Text, await world.AsSuperuserAsync(Reads) as string));
    }

    [DatabaseFact]
    public async Task AReadingGoesOnWhileTheChainIsBrokenAndIsRecorded()
    {
        await using var world = await IngestionWorld.CreateAsync();
        using var custody = SyntheticCustody.Holding(Labels);
        var (batch, item) = await SampledAsync(world, custody);
        await BreakAsync(world);

        var read = await ReadAsync(world, custody, batch, item);

        Assert.Equal((ContentReadOutcome.Read, Pseudonymized, "pseudonymized_text_read"), (read.Outcome, read.Text, await world.AsSuperuserAsync(Reads) as string));
    }

    [Fact]
    public async Task AReadingNamedByAnIdentifierThatIsNotPositiveIsRefusedByThatIdentifier()
    {
        await using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=u;Password=p;Database=d");
        var reader = new SampleTextReader(dataSource, null!);

        var refused = new List<string?>
        {
            (await Assert.ThrowsAsync<ArgumentNullException>(() => reader.ReadAsync(null!, TestContext.Current.CancellationToken))).ParamName,
        };
        foreach (var request in new[] { new SampleReading(0, 1, 1, 1), new SampleReading(1, 0, 1, 1), new SampleReading(1, 1, 0, 1), new SampleReading(1, 1, 1, 0) })
        {
            refused.Add((await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reader.ReadAsync(request, TestContext.Current.CancellationToken))).ParamName);
        }

        Assert.Equal(["request", "request.DomainId", "request.BatchId", "request.ItemId", "request.ReaderId"], refused);
    }

    private static Task<ContentReadResult> ReadAsync(BatchesWorld world, SyntheticCustody custody, long batch, long item) =>
        new SampleTextReader(world.DataSource, custody.Content).ReadAsync(new SampleReading(IngestionWorld.Domain, batch, item, IngestionWorld.Person), TestContext.Current.CancellationToken);

    private static async Task<(long Batch, long Item)> SampledAsync(BatchesWorld world, SyntheticCustody custody)
    {
        var batch = await IngestionWorld.OpenAsync(world);
        var written = await new ContentWriter(world.DataSource, custody.Content).AddPseudonymizedItemAsync(
            IngestionWorld.Domain, batch, ItemTexts.PseudonymizedItem("texto bruto zqx", "texto sanitizado zqx", Pseudonymized), TestContext.Current.CancellationToken);
        await DrawAsync(world, batch);
        return (batch, written.ItemId!.Value);
    }

    private static async Task DrawAsync(BatchesWorld world, long batch)
    {
        var drawn = await new BatchReviews(world.DataSource, 1).DrawAsync(new BatchSampling(IngestionWorld.Domain, batch, IngestionWorld.Person), TestContext.Current.CancellationToken);
        Assert.Equal(BatchReviewOutcome.Drawn, drawn.Outcome);
    }

    /// <summary>Breaks the chain as a verification that failed leaves it: an event changed behind the chain, and the verification that finds it.</summary>
    private static async Task BreakAsync(BatchesWorld world)
    {
        _ = await world.AsSuperuserAsync(
            "ALTER TABLE public.custody_event DISABLE TRIGGER custody_event_never_changed; UPDATE public.custody_event SET item_id = item_id + 1 WHERE sequence = 1; "
            + "ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER custody_event_never_changed");
        var failed = await new CustodyVerifier(world.DataSource, TimeProvider.System).VerifyAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, failed.FirstInconsistent?.Sequence);
    }
}
