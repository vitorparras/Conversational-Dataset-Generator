using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Content;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Content;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// The origin of a submission as the area of the batches seals and keeps it (issue 75, part 2b): the declaration carries the sealed text and its key, the batch keeps only the state in clear,
/// and the row of the origin is born with the batch, in its domain, with the key it was sealed under.
/// </summary>
public sealed class BatchOriginSealingTests
{
    private const string Origin = "origem sintetica zqx";
    private const string Hex32 = "0123456789abcdef0123456789abcdef";
    private const string Hex64 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string OriginState = "SELECT origin_state FROM content.ingestion_batch WHERE id = @id";
    private const string Counted = "SELECT (SELECT count(*) FROM content.replication_occurrence) || ',' || (SELECT count(*) FROM content.replication_mark)";
    private const string OriginRow = "SELECT domain_id || '|' || batch_id || '|' || dek_epoch || '|' || length(origin) || '|' || length(dek_wrapped) FROM content.batch_origin";

    private static readonly string[] Labels = ["texto-bruto/1/v1", "mapa-reversao/1/v1", "texto-bruto/2/v1", "mapa-reversao/2/v1"];

    private static readonly DateTimeOffset Said = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ADeclaredOriginCarriesTheSealedTextAndTheKeyItWasSealedUnder()
    {
        using var custody = SyntheticCustody.Holding(Labels);

        var declared = OriginDeclaration.Declared(custody.Content, IngestionWorld.Domain, Origin);

        Assert.Equal(("declared", 1), (declared.State, declared.Key?.Epoch));
        Assert.NotEmpty(declared.Content!.Envelope.ToArray());
        Assert.NotEmpty(declared.Key!.Wrapped.ToArray());
    }

    [Fact]
    public void TheStateOfEachDeclarationIsTheOneItsSubmissionChose()
    {
        using var custody = SyntheticCustody.Holding(Labels);

        Assert.Equal(
            ("unknown", "declared", "declared"),
            (OriginDeclaration.Unknown.State, OriginDeclaration.DeclaredNotKept.State, OriginDeclaration.Declared(custody.Content, IngestionWorld.Domain, Origin).State));
    }

    [Fact]
    public void ADeclaredOriginNeedsThePathThatSealsAndATextWithSomethingInItUpToTheCeiling()
    {
        using var custody = SyntheticCustody.Holding(Labels);

        string?[] refused =
        [
            Assert.Throws<ArgumentNullException>(() => OriginDeclaration.Declared(null!, IngestionWorld.Domain, Origin)).ParamName,
            Assert.Throws<ArgumentNullException>(() => OriginDeclaration.Declared(custody.Content, IngestionWorld.Domain, null!)).ParamName,
            Assert.Throws<ArgumentException>(() => OriginDeclaration.Declared(custody.Content, IngestionWorld.Domain, "  ")).ParamName,
            Assert.Throws<ArgumentOutOfRangeException>(() => OriginDeclaration.Declared(custody.Content, IngestionWorld.Domain, new string('a', OriginDeclaration.MostCharacters + 1))).ParamName,
        ];

        Assert.Equal(["sealing", "text", "text", "text.Length"], refused);
        Assert.Equal("declared", OriginDeclaration.Declared(custody.Content, IngestionWorld.Domain, new string('a', OriginDeclaration.MostCharacters)).State);
    }

    [DatabaseFact]
    public async Task ABatchOpenedWithADeclaredOriginKeepsTheRowOfTheOriginInItsDomainUnderTheKeyItWasSealedUnder()
    {
        await using var world = await IngestionWorld.CreateAsync();
        using var custody = SyntheticCustody.Holding(Labels);
        var declared = OriginDeclaration.Declared(custody.Content, IngestionWorld.Domain, Origin);

        var batch = await OpenAsync(world, declared);
        var stored = (string?)await world.InDomainAsync(IngestionWorld.Domain, OriginRow);

        Assert.Equal($"{IngestionWorld.Domain}|{batch}|{declared.Key!.Epoch}|{declared.Content!.Envelope.Length}|{declared.Key.Wrapped.Length}", stored);
        Assert.Equal("declared", await world.InDomainAsync(IngestionWorld.Domain, OriginState, ("id", batch)));
        Assert.Equal(declared.Content.Envelope.ToArray(), (byte[]?)await world.InDomainAsync(IngestionWorld.Domain, "SELECT origin FROM content.batch_origin WHERE batch_id = @id", ("id", batch)));
        Assert.Equal(declared.Key.Wrapped.ToArray(), (byte[]?)await world.InDomainAsync(IngestionWorld.Domain, "SELECT dek_wrapped FROM content.batch_origin WHERE batch_id = @id", ("id", batch)));
    }

    [DatabaseFact]
    public async Task ABatchWhoseOriginIsUnknownOrWasNotKeptHoldsNoRowOfTheOriginAndOnlyTheState()
    {
        await using var world = await IngestionWorld.CreateAsync();

        var unknown = await OpenAsync(world, OriginDeclaration.Unknown);
        var notKept = await OpenAsync(world, OriginDeclaration.DeclaredNotKept, new string('1', 32));

        var states = (await StateAsync(world, unknown), await StateAsync(world, notKept));
        var rows = await world.InDomainAsync(IngestionWorld.Domain, "SELECT count(*)::text FROM content.batch_origin") as string;

        Assert.Equal((("unknown", "declared"), "0"), (states, rows));
    }

    [DatabaseFact]
    public async Task ABatchThatIsRejectedTakesTheMarksAndTheOccurrencesOfItsSubmissionWithIt()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        var item = await IngestionWorld.AddItemAsync(world, batch);
        var occurrence = new StoredOccurrence(item, 0, new string('a', 64), new string('b', 64), 40, Said);
        await new ReplicationStore(world.DataSource).RecordAsync(
            new ReplicationTarget(IngestionWorld.Domain, batch), [occurrence], [new StoredMark(item, 0, item, 0)], TestContext.Current.CancellationToken);
        var before = await CountsAsync(world);

        var rejected = await IngestionWorld.RejectAsync(world, batch, "invalid_rows");

        Assert.Equal((BatchOutcome.Rejected, "1,1", "0,0"), (rejected.Outcome, before, await CountsAsync(world)));
    }

    private static async Task<string?> StateAsync(BatchesWorld world, long batch) =>
        await world.InDomainAsync(IngestionWorld.Domain, OriginState, ("id", batch)) as string;

    [SuppressMessage("Security", "CA2100", Justification = "The statement is a literal of the test, never input.")]
    private static async Task<object?> CountsAsync(BatchesWorld world)
    {
        await using var connection = await Sql.OpenAsync(world.Database.SuperuserConnectionString);
        await using var command = new NpgsqlCommand(Counted, connection);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<long> OpenAsync(BatchesWorld world, OriginDeclaration origin, string submission = Hex32)
    {
        var opened = await IngestionWorld.BatchesOf(world).OpenAsync(
            new BatchOpening(IngestionWorld.Domain, IngestionWorld.Person, null, new SubmissionOpening(new SubmissionReceipt(submission, 1, Hex64), origin)), TestContext.Current.CancellationToken);
        Assert.Equal(BatchOutcome.Opened, opened.Outcome);
        return opened.BatchId!.Value;
    }
}
