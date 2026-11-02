using Core.Persistence.Custody;
using Core.Persistence.Custody.Verification;
using Core.Persistence.Tests.Custody;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Verification;

/// <summary>
/// The verification of a chain that holds events with and without a reference (#73): the digests of the events written before the reference are what they were, an emptying carries its
/// reference in the digest, and a reference changed outside the system is the first event that does not hold.
/// </summary>
[Trait("Category", "Database")]
public sealed class CustodyVerifierReferenceTests
{
    private const string Untrusted = "ALTER TABLE public.custody_event DISABLE TRIGGER custody_event_never_changed; ";
    private const string Trusted = "; ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER custody_event_never_changed";

    [DatabaseFact]
    public async Task ChainOfEventsWithAndWithoutAReferenceIsWhole()
    {
        await using var database = new CustodyDatabase();
        await AppendOldAndNewEventsAsync(database);

        var verification = await (await VerifierAsync(database)).VerifyAsync(TestContext.Current.CancellationToken);

        Assert.Equal((true, 4L, null), (verification.Passed, verification.EventsRead, verification.FirstInconsistent));
    }

    [DatabaseFact]
    public async Task ReferenceChangedOutsideTheSystemIsTheFirstEventThatDoesNotHold()
    {
        await using var database = new CustodyDatabase();
        await AppendOldAndNewEventsAsync(database);
        await database.RunAsSuperuserAsync($"{Untrusted}UPDATE public.custody_event SET reference = 'PROT-OUTRO' WHERE sequence = 3{Trusted}");

        var verification = await (await VerifierAsync(database)).VerifyAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new InconsistentEvent(3, ChainInconsistency.DigestMismatch), verification.FirstInconsistent);
    }

    private static async Task<CustodyVerifier> VerifierAsync(CustodyDatabase database) =>
        new(await database.DataSourceAsync(), TimeProvider.System);

    private static async Task AppendOldAndNewEventsAsync(CustodyDatabase database)
    {
        var chain = await database.ChainAsync();
        var person = await database.PersonAsync(active: true);
        var author = CustodyAuthor.Person(person);
        var item = new CustodyItem(CustodyItemKind.Person, person);
        var events = new[]
        {
            CustodyEvent.Of(author, item, CustodyStep.PersonCreated, CustodyReason.AdministrativeAct),
            CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.ItemText, 9), CustodyStep.RawTextRead, CustodyReason.ContentRead),
            CustodyEvent.Emptied(author, 9, CustodyReason.RemovalRequestedBySubject, "PROT-2026/0042"),
            CustodyEvent.EmptyingRefused(CustodyAuthor.System, 9, CustodyReason.ItemAlreadyEmptied, "PROT-2026/0043"),
        };
        foreach (var custodyEvent in events)
        {
            Assert.Equal(CustodyOutcome.Appended, (await chain.AppendAsync(custodyEvent, TestContext.Current.CancellationToken)).Outcome);
        }
    }
}
