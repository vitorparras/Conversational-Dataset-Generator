using Core.Persistence.Custody;
using Core.Persistence.Custody.Verification;
using Core.Persistence.Tests.Custody;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Verification;

/// <summary>
/// Two verifications that meet (#61, review of the PR): one takes its view while the chain holds, the chain is changed outside the system, the other fails and records the failure, and only then
/// the first records its verdict, which is "passed" about a view older than the failure. The chain refuses that verdict and the refusal of writing stays; a verification that reads the failure
/// and passes, with the chain put back, lifts it. The late verdict is added the way the verifier adds its own: through the chain, with the number of events its view held.
/// </summary>
[Trait("Category", "Database")]
public sealed class CustodyVerifierRaceTests
{
    private const string Untrusted = "ALTER TABLE public.custody_event DISABLE TRIGGER custody_event_never_changed; ";
    private const string Trusted = "; ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER custody_event_never_changed";
    private const string Change = $"{Untrusted}UPDATE public.custody_event SET item_id = item_id + 1 WHERE sequence = 2{Trusted}";
    private const string PutBack = $"{Untrusted}UPDATE public.custody_event SET item_id = item_id - 1 WHERE sequence = 2{Trusted}";
    private const int EventsOfTheOlderView = 3;

    [DatabaseFact]
    public async Task PassedVerdictAboutAViewOlderThanTheRecordedFailureIsRefusedAndTheChainStaysBroken()
    {
        await using var database = new CustodyDatabase();
        var chain = await BrokenByTheOtherVerificationAsync(database);

        var late = await chain.AppendAsync(CustodyEvent.Verification(EventsOfTheOlderView, null), TestContext.Current.CancellationToken);
        var ordinary = await chain.AppendAsync(Ordinary(), TestContext.Current.CancellationToken);

        Assert.Equal(CustodyOutcome.StaleVerdict, late.Outcome);
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

    [DatabaseFact]
    public async Task ChainPutBackThatPassesAgainReadingTheFailureLiftsTheRefusal()
    {
        await using var database = new CustodyDatabase();
        var chain = await BrokenByTheOtherVerificationAsync(database);
        _ = await chain.AppendAsync(CustodyEvent.Verification(EventsOfTheOlderView, null), TestContext.Current.CancellationToken);
        await database.RunAsSuperuserAsync(PutBack);

        var passed = await Verifier(database).VerifyAsync(TestContext.Current.CancellationToken);
        var ordinary = await chain.AppendAsync(Ordinary(), TestContext.Current.CancellationToken);

        Assert.Equal((true, 4L, CustodyOutcome.Appended), (passed.Passed, passed.EventsRead, passed.Recorded.Outcome));
        Assert.Equal(CustodyOutcome.Appended, ordinary.Outcome);
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
        var failed = await Verifier(database).VerifyAsync(TestContext.Current.CancellationToken);
        Assert.Equal((false, 2L, CustodyOutcome.Appended), (failed.Passed, failed.FirstInconsistent?.Sequence, failed.Recorded.Outcome));
        return chain;
    }

    private static CustodyVerifier Verifier(CustodyDatabase database) => new(database.DataSourceAsync().GetAwaiter().GetResult(), TimeProvider.System);

    private static CustodyEvent Ordinary() =>
        CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.Person, 7403), CustodyStep.PersonCreated, CustodyReason.AdministrativeAct);
}
