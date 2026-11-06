using System.Globalization;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Custody.Verification;
using Core.Persistence.Recovery;
using Core.Persistence.Tests.Content;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Recovery;

/// <summary>
/// The record of the rehearsal in the chain of the production (issue 67, CA-3): the step is written only if the top of the copy is in this chain with its very digest and the proof of the key is
/// the one of the custody here, and otherwise nothing is written and the answer says which of the two failed.
/// </summary>
[Trait("Category", "Database")]
public sealed class RecoveryRecordingTests
{
    private const string Proof = "0123456789abcdef";
    private const string Tip = "SELECT sequence || ',' || encode(digest, 'hex') FROM public.custody_event ORDER BY sequence DESC LIMIT 1";
    private const string Written = "SELECT count(*) FROM public.custody_event WHERE step = 'recovery_tested'";

    [DatabaseFact]
    public async Task Ca3TheStepIsWrittenWhenTheTopOfTheCopyIsInTheChainAndTheKeyIsTheSameAndTheChainVerifies()
    {
        await using var world = await WorldWithAChainAsync();
        var receipt = await ReceiptOfTheTopAsync(world, Proof, new SampleCounts(2, 2, 0));

        var record = await Recording(world).RecordAsync(receipt.ToText(), Proof, TestContext.Current.CancellationToken);

        Assert.Equal((RecoveryOutcome.Recorded, 0, 1L), (record.Outcome, record.Failures.Count, await world.ScalarAsync(Written)));
        Assert.Equal($"master_key,{receipt.TopSequence},recovery_tested,administrative_act", await world.ScalarAsync(
            "SELECT item_kind || ',' || item_id || ',' || step || ',' || reason FROM public.custody_event WHERE step = 'recovery_tested'"));
        Assert.True((await new CustodyVerifier(world.DataSource, TimeProvider.System).VerifyAsync(TestContext.Current.CancellationToken)).Passed);
    }

    [DatabaseFact]
    public async Task Ca3ATopThatIsNotInTheChainRefusesNamingTheTopAndWritesNothing()
    {
        await using var world = await WorldWithAChainAsync();
        var receipt = (await ReceiptOfTheTopAsync(world, Proof, new SampleCounts(1, 1, 0))) with { TopDigest = new string('0', 64) };

        var record = await Recording(world).RecordAsync(receipt.ToText(), Proof, TestContext.Current.CancellationToken);

        Assert.Equal((RecoveryOutcome.NotConfirmed, 0L), (record.Outcome, await world.ScalarAsync(Written)));
        Assert.Equal("o topo da cópia não está na cadeia da produção", Assert.Single(record.Failures));
    }

    [DatabaseFact]
    public async Task Ca3ATopBeyondTheEndOfTheChainRefusesNamingTheTop()
    {
        await using var world = await WorldWithAChainAsync();
        var receipt = (await ReceiptOfTheTopAsync(world, Proof, new SampleCounts(1, 1, 0))) with { TopSequence = 9999 };

        var record = await Recording(world).RecordAsync(receipt.ToText(), Proof, TestContext.Current.CancellationToken);

        Assert.Equal(("o topo da cópia não está na cadeia da produção", 0L), (Assert.Single(record.Failures), await world.ScalarAsync(Written)));
    }

    [DatabaseFact]
    public async Task Ca3AKeyThatDoesNotMatchRefusesNamingTheKeyAndWritesNothing()
    {
        await using var world = await WorldWithAChainAsync();
        var receipt = await ReceiptOfTheTopAsync(world, Proof, new SampleCounts(1, 1, 0));

        var record = await Recording(world).RecordAsync(receipt.ToText(), "fedcba9876543210", TestContext.Current.CancellationToken);

        Assert.Equal((RecoveryOutcome.NotConfirmed, 0L), (record.Outcome, await world.ScalarAsync(Written)));
        Assert.Equal("a chave do ensaio não é a do arquivo de custódia daqui", Assert.Single(record.Failures));
    }

    [DatabaseFact]
    public async Task Ca3WhenBothFailTheAnswerNamesBothAndWritesNothing()
    {
        await using var world = await WorldWithAChainAsync();
        var receipt = (await ReceiptOfTheTopAsync(world, Proof, new SampleCounts(1, 1, 0))) with { TopSequence = 9999 };

        var record = await Recording(world).RecordAsync(receipt.ToText(), "fedcba9876543210", TestContext.Current.CancellationToken);

        Assert.Equal((2, 0L), (record.Failures.Count, await world.ScalarAsync(Written)));
    }

    [DatabaseFact]
    public async Task Ca3AReceiptOfARehearsalThatDidNotHoldIsNotRecorded()
    {
        await using var world = await WorldWithAChainAsync();
        var receipt = await ReceiptOfTheTopAsync(world, Proof, new SampleCounts(1, 1, 2));

        var record = await Recording(world).RecordAsync(receipt.ToText(), Proof, TestContext.Current.CancellationToken);

        var written = await world.ScalarAsync(Written);

        Assert.Equal((RecoveryOutcome.NotConfirmed, 0L, "o ensaio teve amostras que não abriram, ou nenhuma abriu"), (record.Outcome, written, Assert.Single(record.Failures)));
    }

    [DatabaseTheory]
    [InlineData("")]
    [InlineData("zqx")]
    [InlineData("CDG-ENSAIO-1|2031-03-04T08:06:07Z|1|abc")]
    public async Task Ca2AReceiptThatDoesNotHoldIsRefusedWithTheReasonAndNothingIsWritten(string text)
    {
        await using var world = await WorldWithAChainAsync();

        var record = await Recording(world).RecordAsync(text, Proof, TestContext.Current.CancellationToken);

        Assert.Equal((RecoveryOutcome.ReceiptRefused, true, 0L), (record.Outcome, !string.IsNullOrWhiteSpace(Assert.Single(record.Failures)), await world.ScalarAsync(Written)));
    }

    [DatabaseFact]
    public async Task Ca2AReceiptAlteredByADigitIsRefusedAsAlteredAndNothingIsWritten()
    {
        await using var world = await WorldWithAChainAsync();
        var receipt = await ReceiptOfTheTopAsync(world, Proof, new SampleCounts(1, 1, 0));
        var altered = receipt.ToText().Replace($"|{receipt.TopSequence}|", $"|{receipt.TopSequence + 1}|", StringComparison.Ordinal);

        var record = await Recording(world).RecordAsync(altered, Proof, TestContext.Current.CancellationToken);

        Assert.Equal((RecoveryOutcome.ReceiptRefused, 0L), (record.Outcome, await world.ScalarAsync(Written)));
        Assert.Contains("alterado", Assert.Single(record.Failures), StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task Ca584TheEventCarriesTheDigestOfTheReceiptItRecords()
    {
        await using var world = await WorldWithAChainAsync();
        var receipt = await ReceiptOfTheTopAsync(world, Proof, new SampleCounts(2, 2, 0));

        _ = await Recording(world).RecordAsync(receipt.ToText(), Proof, TestContext.Current.CancellationToken);

        Assert.Equal(receipt.Digest(), await world.ScalarAsync("SELECT reference FROM public.custody_event WHERE step = 'recovery_tested'"));
        Assert.Matches("^[0-9a-f]{64}$", receipt.Digest());
    }

    [DatabaseFact]
    public async Task Ca584ARepeatedReceiptIsRefusedWithTheReasonAndTheChainDoesNotChange()
    {
        await using var world = await WorldWithAChainAsync();
        var receipt = await ReceiptOfTheTopAsync(world, Proof, new SampleCounts(2, 2, 0));
        var first = await Recording(world).RecordAsync(receipt.ToText(), Proof, TestContext.Current.CancellationToken);
        var before = await world.ScalarAsync(Tip);

        var again = await Recording(world).RecordAsync(receipt.ToText(), Proof, TestContext.Current.CancellationToken);

        Assert.Equal((RecoveryOutcome.Recorded, RecoveryOutcome.ReceiptRepeated, 0L, 1L), (first.Outcome, again.Outcome, again.Sequence, await world.ScalarAsync(Written)));
        Assert.Equal((RecoveryRecording.RepeatedWords, before), (Assert.Single(again.Failures), await world.ScalarAsync(Tip)));
        Assert.Contains("já está registrado", RecoveryRecording.RepeatedWords, StringComparison.Ordinal);
        Assert.True((await new CustodyVerifier(world.DataSource, TimeProvider.System).VerifyAsync(TestContext.Current.CancellationToken)).Passed);
    }

    [DatabaseFact]
    public async Task Ca584AReceiptOfAnotherRehearsalOfTheSameTopIsAnotherReceiptAndIsRecorded()
    {
        await using var world = await WorldWithAChainAsync();
        var receipt = await ReceiptOfTheTopAsync(world, Proof, new SampleCounts(2, 2, 0));
        var later = receipt with { At = receipt.At.AddMinutes(1) };

        var first = await Recording(world).RecordAsync(receipt.ToText(), Proof, TestContext.Current.CancellationToken);
        var second = await Recording(world).RecordAsync(later.ToText(), Proof, TestContext.Current.CancellationToken);

        Assert.Equal((RecoveryOutcome.Recorded, RecoveryOutcome.Recorded, 2L), (first.Outcome, second.Outcome, await world.ScalarAsync(Written)));
    }

    [DatabaseFact]
    public async Task Ca584TwoRecordsOfTheSameReceiptAtOnceLeaveOneInTheChainAndTellTheOtherItIsRepeated()
    {
        await using var world = await WorldWithAChainAsync();
        var receipt = await ReceiptOfTheTopAsync(world, Proof, new SampleCounts(2, 2, 0));

        var records = await Task.WhenAll(
            Recording(world).RecordAsync(receipt.ToText(), Proof, TestContext.Current.CancellationToken),
            Recording(world).RecordAsync(receipt.ToText(), Proof, TestContext.Current.CancellationToken));

        Assert.Equal([RecoveryOutcome.Recorded, RecoveryOutcome.ReceiptRepeated], records.Select(record => record.Outcome).Order().ToList());
        Assert.Equal(1L, await world.ScalarAsync(Written));
    }

    [DatabaseFact]
    public async Task Ca584ARepeatedReceiptWithAKeyThatDoesNotMatchIsToldAsNotConfirmed()
    {
        await using var world = await WorldWithAChainAsync();
        var receipt = await ReceiptOfTheTopAsync(world, Proof, new SampleCounts(2, 2, 0));
        _ = await Recording(world).RecordAsync(receipt.ToText(), Proof, TestContext.Current.CancellationToken);

        var again = await Recording(world).RecordAsync(receipt.ToText(), "fedcba9876543210", TestContext.Current.CancellationToken);

        Assert.Equal((RecoveryOutcome.NotConfirmed, 1L), (again.Outcome, await world.ScalarAsync(Written)));
    }

    [DatabaseFact]
    public async Task TheChainThatRefusesTheEventWhileItsLastVerificationFailedIsToldAndNothingIsWritten()
    {
        await using var world = await ContentWorld.CreateAsync();
        await world.BreakTheChainAsync();
        var receipt = await ReceiptOfTheTopAsync(world, Proof, new SampleCounts(1, 1, 0));

        var record = await Recording(world).RecordAsync(receipt.ToText(), Proof, TestContext.Current.CancellationToken);

        Assert.Equal((RecoveryOutcome.ChainRefused, 0L), (record.Outcome, await world.ScalarAsync(Written)));
        Assert.Contains("a cadeia não aceitou o evento", Assert.Single(record.Failures), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NothingIsRecordedWithoutItsPartsAndAnEmptyProofIsADefectOfTheCaller()
    {
        await using var source = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=u;Password=p;Database=d");

        Assert.Equal(
            ["dataSource", "keyProof"],
            [
                Assert.Throws<ArgumentNullException>(() => new RecoveryRecording(null!)).ParamName,
                (await Assert.ThrowsAsync<ArgumentException>(() => new RecoveryRecording(source).RecordAsync("x", string.Empty, TestContext.Current.CancellationToken))).ParamName,
            ]);
    }

    private static RecoveryRecording Recording(ContentWorld world) => new(world.DataSource);

    private static async Task<ContentWorld> WorldWithAChainAsync()
    {
        var world = await ContentWorld.CreateAsync();
        var added = CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.Person, 1), CustodyStep.PersonCreated, CustodyReason.AdministrativeAct);
        Assert.Equal(CustodyOutcome.Appended, (await new CustodyChain(world.DataSource).AppendAsync(added, TestContext.Current.CancellationToken)).Outcome);
        return world;
    }

    private static async Task<RecoveryReceipt> ReceiptOfTheTopAsync(ContentWorld world, string proof, SampleCounts counts)
    {
        var top = ((string)(await world.ScalarAsync(Tip))!).Split(',');
        return new RecoveryReceipt(new DateTimeOffset(2031, 3, 4, 8, 6, 7, TimeSpan.Zero), long.Parse(top[0], CultureInfo.InvariantCulture), top[1], proof, counts);
    }
}
