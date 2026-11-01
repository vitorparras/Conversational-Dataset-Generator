using Core.Persistence.Content;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Verification;
using Core.Persistence.Tests.Custody;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Verification;

/// <summary>
/// The refusal of writing after a verification that failed (#61, CA-5): while the last verdict is a failure nothing is added to the chain but a verification and the reading of content, a
/// verdict that passed is refused until a restoration, content is not written, and the chain still reads and verifies.
/// Each test has a database of its own, because the failing ones leave the chain broken.
/// </summary>
[Trait("Category", "Database")]
public sealed class CustodyBrokenChainTests
{
    private const string Untrusted = "ALTER TABLE public.custody_event DISABLE TRIGGER custody_event_never_changed; ";
    private const string Trusted = "; ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER custody_event_never_changed";
    private const string RefusedByTheBrokenChain = "CE002";

    // What the restoration brings: the state of a copy, which never had the verdict of the failure.
    private const string Forgotten = "DELETE FROM public.custody_event WHERE step = 'verify_chain' AND reason = 'verification_failed'";

    private static readonly DateTimeOffset Moment = new(2031, 3, 4, 5, 6, 7, TimeSpan.Zero);

    [DatabaseFact]
    public async Task AfterAFailureAnOrdinaryAppendIsRefusedAsANamedResultAndTheChainStillReadsAndVerifies()
    {
        await using var database = new CustodyDatabase();
        await BreakAsync(database);
        var chain = await database.ChainAsync();

        var refused = await chain.AppendAsync(Ordinary(), TestContext.Current.CancellationToken);
        var again = await (await VerifierAsync(database)).VerifyAsync(TestContext.Current.CancellationToken);

        Assert.Equal((CustodyOutcome.ChainBroken, null), (refused.Outcome, refused.Sequence));
        Assert.Equal((false, CustodyOutcome.Appended), (again.Passed, again.Recorded.Outcome));
        Assert.Equal(5, (await database.EventsAsync()).Count);
    }

    [DatabaseFact]
    public async Task VerdictThatPassedOnAViewOlderThanTheLastFailureIsRefusedAndTheRefusalStays()
    {
        await using var database = new CustodyDatabase();
        await BreakAsync(database);
        var chain = await database.ChainAsync();

        var late = await chain.AppendAsync(CustodyEvent.Verification(3, null), TestContext.Current.CancellationToken);
        var ordinary = await chain.AppendAsync(Ordinary(), TestContext.Current.CancellationToken);

        Assert.Equal((CustodyOutcome.PassedAfterFailure, null, CustodyOutcome.ChainBroken), (late.Outcome, late.Sequence, ordinary.Outcome));
        Assert.Equal(4, (await database.EventsAsync()).Count);
    }

    [DatabaseFact]
    public async Task PassedAfterFailureIsRefusedByTheServerWithItsOwnStateAndNamingNoEvent()
    {
        await using var database = new CustodyDatabase();
        await BreakAsync(database);

        var refused = await database.RefusedToAppAsync(
            "INSERT INTO public.custody_event (actor_system, item_kind, item_id, step, reason) VALUES (true, 'custody_chain', 3, 'verify_chain', 'verification_passed')");

        Assert.Equal(("CE003", false), (refused.SqlState, refused.MessageText.Contains('3', StringComparison.Ordinal)));
    }

    [DatabaseFact]
    public async Task VerdictThatPassedAfterTheFailureIsRefusedWhateverViewItWasMadeOnAndWritingStaysBlocked()
    {
        await using var database = new CustodyDatabase();
        await BreakAsync(database);
        var chain = await database.ChainAsync();

        var fresh = await chain.AppendAsync(CustodyEvent.Verification(4, null), TestContext.Current.CancellationToken);
        var ordinary = await chain.AppendAsync(Ordinary(), TestContext.Current.CancellationToken);

        Assert.Equal((CustodyOutcome.PassedAfterFailure, CustodyOutcome.ChainBroken, 4), (fresh.Outcome, ordinary.Outcome, (await database.EventsAsync()).Count));
    }

    [DatabaseFact]
    public async Task ChainPutBackButWithTheFailureStillOnItPassesTheCheckButIsNotRecordedAsPassedSoWritingStaysBlocked()
    {
        await using var database = new CustodyDatabase();
        await BreakAsync(database);
        await database.RunAsSuperuserAsync($"{Untrusted}UPDATE public.custody_event SET item_id = item_id - 1 WHERE sequence = 2{Trusted}");

        var verification = await (await VerifierAsync(database)).VerifyAsync(TestContext.Current.CancellationToken);
        var ordinary = await (await database.ChainAsync()).AppendAsync(Ordinary(), TestContext.Current.CancellationToken);

        Assert.Equal((true, CustodyOutcome.PassedAfterFailure, CustodyOutcome.ChainBroken), (verification.Passed, verification.Recorded.Outcome, ordinary.Outcome));
    }

    [DatabaseTheory]
    [InlineData(CustodyStep.RawTextRead)]
    [InlineData(CustodyStep.SanitizedTextRead)]
    [InlineData(CustodyStep.ReversalEntriesRead)]
    public async Task AfterAFailureTheReadingOfContentIsStillRecordedBecauseTheReadingIsADefectToStopAndNotToHide(CustodyStep step)
    {
        await using var database = new CustodyDatabase();
        await BreakAsync(database);
        var chain = await database.ChainAsync();
        var reading = CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.ItemText, 9), step, CustodyReason.ContentRead);

        var recorded = await chain.AppendAsync(reading, TestContext.Current.CancellationToken);

        Assert.Equal((CustodyOutcome.Appended, 5L), (recorded.Outcome, recorded.Sequence));
    }

    [DatabaseTheory]
    [InlineData(CustodyStep.PersonCreated)]
    [InlineData(CustodyStep.PersonDeactivated)]
    [InlineData(CustodyStep.PersonFunctionChanged)]
    [InlineData(CustodyStep.PersonPasswordSet)]
    public async Task AfterAFailureEveryOtherStepIsStillRefusedAndOnlyTheReadingsAreLetIn(CustodyStep step)
    {
        await using var database = new CustodyDatabase();
        await BreakAsync(database);
        var chain = await database.ChainAsync();
        var other = CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.Person, 7403), step, CustodyReason.AdministrativeAct);

        var refused = await chain.AppendAsync(other, TestContext.Current.CancellationToken);

        Assert.Equal((CustodyOutcome.ChainBroken, 4), (refused.Outcome, (await database.EventsAsync()).Count));
    }

    [DatabaseFact]
    public async Task AfterAFailureTheServerRefusesAnAppendFromOutsideTheApplicationWithoutNamingAnyEvent()
    {
        await using var database = new CustodyDatabase();
        await BreakAsync(database);

        var refused = await database.RefusedToAppAsync(
            "INSERT INTO public.custody_event (actor_system, item_kind, item_id, step, reason) VALUES (true, 'person', 7403, 'person_created', 'administrative_act')");

        Assert.Equal(RefusedByTheBrokenChain, refused.SqlState);
        Assert.DoesNotContain("7403", refused.MessageText, StringComparison.Ordinal);
        Assert.DoesNotContain("person_created", refused.MessageText, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task AfterAFailureContentIsNotWrittenAndTheRefusalIsTheSameWhateverTheTable()
    {
        await using var database = new CustodyDatabase();
        await BreakAsync(database);
        await database.RunAsOwnerAsync("INSERT INTO public.domain (name) VALUES ('zqx-dominio')");
        var source = await database.DataSourceAsync();

        var refused = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var transaction = await ContentTransaction.BeginAsync(source, 1, TestContext.Current.CancellationToken);
            await using var command = transaction.CreateCommand("INSERT INTO content.item_text (domain_id, raw_text) VALUES (1, '\\x00')");
            _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        });

        Assert.Equal(RefusedByTheBrokenChain, refused.SqlState);
    }

    [DatabaseFact]
    public async Task ChainRestoredWithoutTheFailureAndVerifiedAgainLiftsTheRefusal()
    {
        await using var database = new CustodyDatabase();
        await AppendEventsAsync(database, 3);
        await database.RunAsSuperuserAsync($"{Untrusted}UPDATE public.custody_event SET item_id = item_id + 1 WHERE sequence = 2{Trusted}");
        var verifier = await VerifierAsync(database);
        _ = await verifier.VerifyAsync(TestContext.Current.CancellationToken);
        await database.RunAsSuperuserAsync($"{Untrusted}UPDATE public.custody_event SET item_id = item_id - 1 WHERE sequence = 2; {Forgotten}{Trusted}");

        var passed = await verifier.VerifyAsync(TestContext.Current.CancellationToken);
        var appended = await (await database.ChainAsync()).AppendAsync(Ordinary(), TestContext.Current.CancellationToken);

        Assert.Equal((true, 3L), (passed.Passed, passed.EventsRead));
        Assert.Equal(CustodyOutcome.Appended, appended.Outcome);
    }

    private static async Task<CustodyVerifier> VerifierAsync(CustodyDatabase database) =>
        new(await database.DataSourceAsync(), new FixedTime(Moment));

    private static async Task AppendEventsAsync(CustodyDatabase database, int count)
    {
        var chain = await database.ChainAsync();
        for (var item = 1; item <= count; item++)
        {
            _ = await AppendAsync(chain, CustodyAuthor.System, new CustodyItem(CustodyItemKind.Person, item), CustodyStep.PersonCreated);
        }
    }

    /// <summary>Three events, one changed outside the system, and a verification that failed and was recorded.</summary>
    private static async Task BreakAsync(CustodyDatabase database)
    {
        await AppendEventsAsync(database, 3);
        await database.RunAsSuperuserAsync($"{Untrusted}UPDATE public.custody_event SET item_id = item_id + 1 WHERE sequence = 2{Trusted}");
        var failed = await (await VerifierAsync(database)).VerifyAsync(TestContext.Current.CancellationToken);
        Assert.False(failed.Passed);
    }

    private static Task<CustodyResult> AppendAsync(CustodyChain chain, CustodyAuthor author, CustodyItem item, CustodyStep step) =>
        chain.AppendAsync(CustodyEvent.Of(author, item, step, CustodyReason.AdministrativeAct), TestContext.Current.CancellationToken);

    private static CustodyEvent Ordinary() =>
        CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.Person, 7403), CustodyStep.PersonCreated, CustodyReason.AdministrativeAct);

    private sealed class FixedTime(DateTimeOffset moment) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => moment;
    }
}
