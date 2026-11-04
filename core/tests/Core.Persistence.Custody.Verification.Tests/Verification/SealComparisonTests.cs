using Core.Persistence.Custody;
using Core.Persistence.Custody.Verification;
using Core.Persistence.Tests.Custody;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Verification;

/// <summary>
/// The chain against the seal someone brought from outside (#62, CA-3, CA-4 and CA-5): a base replaced by another coherent one, and a chain with its last events taken away, both pass the
/// links and fail the seal, each saying which; a chain that only grew holds.
/// </summary>
[Trait("Category", "Database")]
public sealed class SealComparisonTests
{
    private const string Untrusted = "ALTER TABLE public.custody_event DISABLE TRIGGER custody_event_never_changed; ";
    private const string Trusted = "; ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER custody_event_never_changed";

    private static readonly DateTimeOffset Moment = new(2031, 3, 4, 5, 6, 7, TimeSpan.Zero);

    [DatabaseFact]
    public async Task BaseReplacedByAnotherThatIsCoherentPassesTheLinksAndFailsTheSealSayingTheDigestIsDifferent()
    {
        await using var original = new CustodyDatabase();
        await using var replacement = new CustodyDatabase();
        await AppendEventsAsync(original, 4, firstItem: 1);
        await AppendEventsAsync(replacement, 4, firstItem: 101);
        var seal = (await RecordSealAsync(original)).Seal!;

        var verification = await VerifyAsync(replacement, seal);

        Assert.Equal((true, SealOutcome.DigestDiffers, false), (verification.Passed, verification.Seal!.Outcome, verification.Seal.Holds));
        Assert.StartsWith("Resumo diferente", verification.Seal.Statement, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task BaseReplacedByALongerOneIsAlsoToldByTheDigest()
    {
        await using var original = new CustodyDatabase();
        await using var replacement = new CustodyDatabase();
        await AppendEventsAsync(original, 3, firstItem: 1);
        await AppendEventsAsync(replacement, 7, firstItem: 101);
        var seal = (await RecordSealAsync(original)).Seal!;

        var verification = await VerifyAsync(replacement, seal);

        Assert.Equal((true, SealOutcome.DigestDiffers), (verification.Passed, verification.Seal!.Outcome));
    }

    [DatabaseFact]
    public async Task LastEventsTakenAwayLeaveACoherentPrefixThatPassesTheLinksAndFailsTheSealSayingTheCountIsLower()
    {
        await using var database = new CustodyDatabase();
        await AppendEventsAsync(database, 5, firstItem: 1);
        var seal = (await RecordSealAsync(database)).Seal!;
        await database.RunAsSuperuserAsync($"{Untrusted}DELETE FROM public.custody_event WHERE sequence > 3{Trusted}");

        var verification = await VerifyAsync(database, seal);

        Assert.Equal((true, 3L, SealOutcome.CountBelowSealed), (verification.Passed, verification.EventsRead, verification.Seal!.Outcome));
        Assert.StartsWith("Contagem menor que a selada", verification.Seal.Statement, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task SealedEventAlteredWithItsColumnOfDigestLeftAsItWasIsNotTheSealedOneAndTheLinksSayTheSame()
    {
        await using var database = new CustodyDatabase();
        await AppendEventsAsync(database, 4, firstItem: 1);
        var seal = (await RecordSealAsync(database)).Seal!;
        await database.RunAsSuperuserAsync($"{Untrusted}UPDATE public.custody_event SET item_id = item_id + 1 WHERE sequence = {seal.Events}{Trusted}");

        var verification = await VerifyAsync(database, seal);

        Assert.Equal((new InconsistentEvent(seal.Events, ChainInconsistency.DigestMismatch), SealOutcome.DigestDiffers), (verification.FirstInconsistent, verification.Seal!.Outcome));
    }

    [DatabaseFact]
    public async Task ChainThatGrewAfterTheSealPassesTheLinksAndHoldsTheSeal()
    {
        await using var database = new CustodyDatabase();
        await AppendEventsAsync(database, 3, firstItem: 1);
        var seal = (await RecordSealAsync(database)).Seal!;
        await AppendEventsAsync(database, 4, firstItem: 50);

        var verification = await VerifyAsync(database, seal);

        Assert.Equal((true, SealOutcome.Holds, true), (verification.Passed, verification.Seal!.Outcome, verification.Seal.Holds));
    }

    [DatabaseFact]
    public async Task SealReadBackFromItsTextIsComparedLikeTheSealItCameFrom()
    {
        await using var database = new CustodyDatabase();
        await AppendEventsAsync(database, 3, firstItem: 1);
        var text = (await RecordSealAsync(database)).Seal!.ToText();

        var verification = await VerifyAsync(database, ChainSeal.Read(text).Seal!);

        Assert.True(verification.Seal!.Holds);
    }

    [DatabaseFact]
    public async Task ChainSealedWithNoEventsHoldsTheSealWhateverItHoldsLater()
    {
        await using var database = new CustodyDatabase();
        var seal = (await RecordSealAsync(database)).Seal!;
        await AppendEventsAsync(database, 2, firstItem: 1);

        var verification = await VerifyAsync(database, seal);

        Assert.Equal((true, 0L), (verification.Seal!.Holds, verification.Seal.Sealed.Events));
    }

    [DatabaseFact]
    public async Task LinksThatFailAndASealThatHoldsAreToldApartAndTheVerdictOfTheLinksIsTheOneRecorded()
    {
        await using var database = new CustodyDatabase();
        await AppendEventsAsync(database, 4, firstItem: 1);
        var seal = (await RecordSealAsync(database)).Seal!;
        await database.RunAsSuperuserAsync($"{Untrusted}UPDATE public.custody_event SET item_id = item_id + 1 WHERE sequence = 2{Trusted}");

        var verification = await VerifyAsync(database, seal);

        Assert.Equal((false, new InconsistentEvent(2, ChainInconsistency.DigestMismatch), true), (verification.Passed, verification.FirstInconsistent, verification.Seal!.Holds));
        Assert.Equal("verification_failed", (await database.EventsAsync())[^1].Reason);
    }

    [DatabaseFact]
    public async Task VerifyingWithoutASealComparesNothing()
    {
        await using var database = new CustodyDatabase();
        await AppendEventsAsync(database, 2, firstItem: 1);

        var verification = await new CustodyVerifier(await database.DataSourceAsync(), new FixedTime(Moment)).VerifyAsync(TestContext.Current.CancellationToken);

        Assert.Null(verification.Seal);
    }

    private static async Task<ChainVerification> VerifyAsync(CustodyDatabase database, ChainSeal seal) =>
        await new CustodyVerifier(await database.DataSourceAsync(), new FixedTime(Moment)).VerifyAsync(seal, TestContext.Current.CancellationToken);

    private static async Task<SealRecording> RecordSealAsync(CustodyDatabase database) =>
        await new ChainSealer(await database.DataSourceAsync(), new FixedTime(Moment)).RecordAsync(CustodyAuthor.System, TestContext.Current.CancellationToken);

    private static async Task AppendEventsAsync(CustodyDatabase database, int count, int firstItem)
    {
        var chain = await database.ChainAsync();
        for (var item = firstItem; item < firstItem + count; item++)
        {
            _ = await chain.AppendAsync(
                CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.Person, item), CustodyStep.PersonCreated, CustodyReason.AdministrativeAct),
                TestContext.Current.CancellationToken);
        }
    }

    private sealed class FixedTime(DateTimeOffset moment) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => moment;
    }
}
