using Core.Persistence.Content.Reading;
using Core.Persistence.Content.Writing;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Content;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// The reading of the text of an item of the sample by its own service (issue 822): the text is the sanitized one when it exists and the raw one when it does not, the event of the reading
/// is written once and before the text is opened, an item outside the sample is not found and records nothing, and a reading that meets another writer on the chain is repeated.
/// </summary>
public sealed class SampleTextReaderTests
{
    private const string Reads = "SELECT string_agg(step, ',' ORDER BY sequence) FROM public.custody_event WHERE step IN ('raw_text_read', 'sanitized_text_read')";

    private static readonly string[] Labels = ["texto-bruto/1/v1", "mapa-reversao/1/v1", "texto-bruto/2/v1", "mapa-reversao/2/v1"];

    [DatabaseFact]
    public async Task TheRawTextIsReadWhenNoSanitizedOneExistsAndTheReadingIsRecordedOnce()
    {
        await using var world = await IngestionWorld.CreateAsync();
        using var custody = SyntheticCustody.Holding(Labels);
        var (batch, item) = await SampledAsync(world, custody, new NewItemText("texto bruto zqx"));

        var read = await ReadAsync(world, custody, batch, item);

        Assert.Equal(
            (ContentReadOutcome.Read, "texto bruto zqx", false, item, "raw_text_read"),
            (read.Outcome, read.Text, read.Cleaned, read.RowId, await world.AsSuperuserAsync(Reads) as string));
    }

    [DatabaseFact]
    public async Task TheSanitizedTextIsReadWhenItExistsAndTheReadingSaysSo()
    {
        await using var world = await IngestionWorld.CreateAsync();
        using var custody = SyntheticCustody.Holding(Labels);
        var (batch, item) = await SampledAsync(world, custody, new NewItemText("texto bruto zqx") { Sanitization = ItemTexts.Sanitization("texto sanitizado zqx") });

        var read = await ReadAsync(world, custody, batch, item);

        Assert.Equal(
            (ContentReadOutcome.Read, "texto sanitizado zqx", true, item, "sanitized_text_read"),
            (read.Outcome, read.Text, read.Cleaned, read.RowId, await world.AsSuperuserAsync(Reads) as string));
    }

    [DatabaseFact]
    public async Task EachReadingOfTheSameItemIsOneEventOfItsOwn()
    {
        await using var world = await IngestionWorld.CreateAsync();
        using var custody = SyntheticCustody.Holding(Labels);
        var (batch, item) = await SampledAsync(world, custody, new NewItemText("texto bruto zqx"));

        _ = await ReadAsync(world, custody, batch, item);
        _ = await ReadAsync(world, custody, batch, item);

        Assert.Equal("raw_text_read,raw_text_read", await world.AsSuperuserAsync(Reads) as string);
    }

    [DatabaseFact]
    public async Task AnItemOutsideTheSampleIsNotFoundAndNothingIsRecorded()
    {
        await using var world = await IngestionWorld.CreateAsync();
        using var custody = SyntheticCustody.Holding(Labels);
        var (batch, _) = await SampledAsync(world, custody, new NewItemText("texto bruto zqx"));
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
        var (batch, item) = await SampledAsync(world, custody, new NewItemText("texto bruto zqx"));

        var read = await ChainContention.WhilePendingAsync(world, () => ReadAsync(world, custody, batch, item));

        Assert.Equal((ContentReadOutcome.Read, "texto bruto zqx", "raw_text_read"), (read.Outcome, read.Text, await world.AsSuperuserAsync(Reads) as string));
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

    private static async Task<(long Batch, long Item)> SampledAsync(BatchesWorld world, SyntheticCustody custody, NewItemText text)
    {
        var batch = await IngestionWorld.OpenAsync(world);
        var item = await new ContentWriter(world.DataSource, custody.Content).AddItemTextAsync(IngestionWorld.Domain, batch, text, TestContext.Current.CancellationToken);
        var drawn = await new BatchReviews(world.DataSource, 1).DrawAsync(new BatchSampling(IngestionWorld.Domain, batch, IngestionWorld.Person), TestContext.Current.CancellationToken);
        Assert.Equal(BatchReviewOutcome.Drawn, drawn.Outcome);
        return (batch, item);
    }
}
