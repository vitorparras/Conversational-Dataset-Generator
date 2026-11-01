using Core.Persistence.Custody;
using Core.Persistence.Custody.Verification;
using Core.Persistence.Tests.Custody;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Verification;

/// <summary>
/// Two verifications that meet (#61, review of the PR): one takes its view while the chain holds, the chain is changed outside the system, the other fails and records the failure, and only then
/// the first records its verdict, which is "passed" about a view older than the failure. The chain refuses that verdict and the refusal of writing stays; the restoration that lifts it is
/// tested in <c>CustodyBrokenChainTests</c>.
/// The late verdict is added the way the verifier adds its own: through the chain, with the number of events its view held.
/// </summary>
[Trait("Category", "Database")]
public sealed class CustodyVerifierRaceTests
{
    private const string Untrusted = "ALTER TABLE public.custody_event DISABLE TRIGGER custody_event_never_changed; ";
    private const string Trusted = "; ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER custody_event_never_changed";
    private const string Change = $"{Untrusted}UPDATE public.custody_event SET item_id = item_id + 1 WHERE sequence = 2{Trusted}";
    private const int EventsOfTheOlderView = 3;

    [DatabaseFact]
    public async Task PassedVerdictAboutAViewOlderThanTheRecordedFailureIsRefusedAndTheChainStaysBroken()
    {
        await using var database = new CustodyDatabase();
        var chain = await BrokenByTheOtherVerificationAsync(database);

        var late = await chain.AppendAsync(CustodyEvent.Verification(EventsOfTheOlderView, null), TestContext.Current.CancellationToken);
        var ordinary = await chain.AppendAsync(Ordinary(), TestContext.Current.CancellationToken);

        Assert.Equal(CustodyOutcome.PassedAfterFailure, late.Outcome);
        Assert.Null(late.Sequence);
        Assert.Equal((CustodyOutcome.ChainBroken, 4), (ordinary.Outcome, (await database.EventsAsync()).Count));
        Assert.Equal("verification_failed", (await database.EventsAsync())[^1].Reason);
    }

    [DatabaseFact]
    public async Task LateVerdictIsRefusedAtTheServerToo()
    {
        await using var database = new CustodyDatabase();
        _ = await BrokenByTheOtherVerificationAsync(database);

        var refused = await database.RefusedToAppAsync(
            $"INSERT INTO public.custody_event (actor_system, item_kind, item_id, step, reason) VALUES (true, 'custody_chain', {EventsOfTheOlderView}, 'verify_chain', 'verification_passed')");

        Assert.Equal("CE003", refused.SqlState);
        Assert.DoesNotContain("custody_chain", refused.MessageText, StringComparison.Ordinal);
        Assert.Equal("verification_failed", (await database.EventsAsync())[^1].Reason);
    }

    /// <summary>Three events, one changed outside the system, and the verification that failed and was recorded after the older view was taken.</summary>
    private static async Task<CustodyChain> BrokenByTheOtherVerificationAsync(CustodyDatabase database)
    {
        var chain = await database.ChainAsync();
        for (var item = 1; item <= EventsOfTheOlderView; item++)
        {
            var added = CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.Person, item), CustodyStep.PersonCreated, CustodyReason.AdministrativeAct);
            Assert.Equal(CustodyOutcome.Appended, (await chain.AppendAsync(added, TestContext.Current.CancellationToken)).Outcome);
        }

        await database.RunAsSuperuserAsync(Change);
        var failed = await (await VerifierAsync(database)).VerifyAsync(TestContext.Current.CancellationToken);
        Assert.Equal((false, 2L, CustodyOutcome.Appended), (failed.Passed, failed.FirstInconsistent?.Sequence, failed.Recorded.Outcome));
        return chain;
    }

    private static async Task<CustodyVerifier> VerifierAsync(CustodyDatabase database) => new(await database.DataSourceAsync(), TimeProvider.System);

    private static CustodyEvent Ordinary() =>
        CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.Person, 7403), CustodyStep.PersonCreated, CustodyReason.AdministrativeAct);
}
