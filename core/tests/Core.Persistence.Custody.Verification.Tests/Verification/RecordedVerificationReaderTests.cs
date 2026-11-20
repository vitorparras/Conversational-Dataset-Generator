using Core.Persistence.Custody;
using Core.Persistence.Custody.Verification;
using Core.Persistence.Tests.Custody;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Verification;

/// <summary>
/// The reading of the last verification the chain recorded (#61, part 2): what the chain holds is what is read, a chain with no verification gives none, the verification is found among
/// events written after it, and a verdict the chain refused is not there. Each test has a database of its own.
/// </summary>
[Trait("Category", "Database")]
public sealed class RecordedVerificationReaderTests
{
    private const string Untrusted = "ALTER TABLE public.custody_event DISABLE TRIGGER custody_event_never_changed; ";
    private const string Trusted = "; ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER custody_event_never_changed";

    [DatabaseFact]
    public async Task ChainWithNoVerificationGivesNone()
    {
        await using var database = new CustodyDatabase();
        await AppendEventsAsync(database, 2);

        var last = await (await ReaderAsync(database)).LastAsync(TestContext.Current.CancellationToken);

        Assert.Null(last);
    }

    [DatabaseFact]
    public async Task PassedVerificationIsReadWithItsMomentAndTheCountAndNoInconsistentEvent()
    {
        await using var database = new CustodyDatabase();
        await AppendEventsAsync(database, 3);
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);
        _ = await (await VerifierAsync(database)).VerifyAsync(TestContext.Current.CancellationToken);

        var last = await (await ReaderAsync(database)).LastAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(last);
        Assert.True(last.Passed);
        Assert.Equal((3L, null), (last.EventsRead, last.FirstInconsistent));
        Assert.InRange(last.RecordedAt, before, DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [DatabaseFact]
    public async Task FailedVerificationIsReadPointingAtTheFirstInconsistentEvent()
    {
        await using var database = new CustodyDatabase();
        await AppendEventsAsync(database, 4);
        await database.RunAsSuperuserAsync($"{Untrusted}UPDATE public.custody_event SET item_id = item_id + 1 WHERE sequence = 3{Trusted}");
        _ = await (await VerifierAsync(database)).VerifyAsync(TestContext.Current.CancellationToken);

        var last = await (await ReaderAsync(database)).LastAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(last);
        Assert.False(last.Passed);
        Assert.Equal((4L, 3L), (last.EventsRead, last.FirstInconsistent));
    }

    [DatabaseFact]
    public async Task VerificationIsFoundAmongTheEventsWrittenAfterIt()
    {
        await using var database = new CustodyDatabase();
        await AppendEventsAsync(database, 2);
        _ = await (await VerifierAsync(database)).VerifyAsync(TestContext.Current.CancellationToken);
        await AppendEventsAsync(database, 3);

        var last = await (await ReaderAsync(database)).LastAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(last);
        Assert.Equal((true, 2L), (last.Passed, last.EventsRead));
    }

    [DatabaseFact]
    public async Task LatestOfTwoVerificationsIsTheOneRead()
    {
        await using var database = new CustodyDatabase();
        await AppendEventsAsync(database, 2);
        var verifier = await VerifierAsync(database);
        _ = await verifier.VerifyAsync(TestContext.Current.CancellationToken);
        await AppendEventsAsync(database, 2);
        _ = await verifier.VerifyAsync(TestContext.Current.CancellationToken);

        var last = await (await ReaderAsync(database)).LastAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(last);
        Assert.Equal(5L, last.EventsRead);
    }

    [DatabaseFact]
    public async Task VerdictTheChainRefusedIsNotReadAndTheFailureStaysTheLastOne()
    {
        await using var database = new CustodyDatabase();
        await AppendEventsAsync(database, 3);
        await database.RunAsSuperuserAsync($"{Untrusted}UPDATE public.custody_event SET item_id = item_id + 1 WHERE sequence = 2{Trusted}");
        var verifier = await VerifierAsync(database);
        _ = await verifier.VerifyAsync(TestContext.Current.CancellationToken);
        await database.RunAsSuperuserAsync($"{Untrusted}UPDATE public.custody_event SET item_id = item_id - 1 WHERE sequence = 2{Trusted}");

        var mended = await verifier.VerifyAsync(TestContext.Current.CancellationToken);
        var last = await (await ReaderAsync(database)).LastAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CustodyOutcome.PassedAfterFailure, mended.Recorded.Outcome);
        Assert.NotNull(last);
        Assert.False(last.Passed);
        Assert.Equal(2L, last.FirstInconsistent);
    }

    [DatabaseFact]
    public async Task OpenOwnerWindowIsReadWithItsVersionsTipAndLateness()
    {
        await using var database = new CustodyDatabase();
        await AppendEventsAsync(database, 2);
        _ = await (await database.ChainAsync()).AppendAsync(CustodyEvent.OwnerWindowOpened(15, "from=12;to=15;tip=2;digest=abc;late=1"), TestContext.Current.CancellationToken);

        var open = await (await ReaderAsync(database)).OwnerWindowAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(open);
        Assert.Equal((3L, 12, 15, 2L, true, false), (open.OpenedSequence, open.From, open.To, open.TipSequence, open.AfterTheStep, open.ClosingFailed));
    }

    [DatabaseFact]
    public async Task ReferenceWithAPieceThatIsNotANumberReadsItAsZeroAndTheWindowAsOnTime()
    {
        await using var database = new CustodyDatabase();
        _ = await (await database.ChainAsync()).AppendAsync(CustodyEvent.OwnerWindowOpened(5, "junk;from=4;to=5=6;to=5;tip=x;digest=d"), TestContext.Current.CancellationToken);

        var open = await (await ReaderAsync(database)).OwnerWindowAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(open);
        Assert.Equal((1L, 4, 5, 0L, false), (open.OpenedSequence, open.From, open.To, open.TipSequence, open.AfterTheStep));
    }

    [DatabaseFact]
    public async Task ChainWithNoOwnerWindowGivesNone()
    {
        await using var database = new CustodyDatabase();
        await AppendEventsAsync(database, 2);

        Assert.Null(await (await ReaderAsync(database)).OwnerWindowAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ReaderRefusesANullDataSource() => _ = Assert.Throws<ArgumentNullException>(() => new RecordedVerificationReader(null!));

    private static async Task<RecordedVerificationReader> ReaderAsync(CustodyDatabase database) => new(await database.DataSourceAsync());

    private static async Task<CustodyVerifier> VerifierAsync(CustodyDatabase database) => new(await database.DataSourceAsync(), TimeProvider.System);

    private static async Task AppendEventsAsync(CustodyDatabase database, int count)
    {
        var chain = await database.ChainAsync();
        for (var item = 1; item <= count; item++)
        {
            _ = await chain.AppendAsync(
                CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.Person, item), CustodyStep.PersonCreated, CustodyReason.AdministrativeAct),
                TestContext.Current.CancellationToken);
        }
    }
}
