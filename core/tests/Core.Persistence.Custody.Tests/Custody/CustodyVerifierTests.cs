using Core.Persistence.Custody;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Custody;

/// <summary>
/// The verification of the chain against a server (#61): a whole chain passes with what it declares and the verdict is an event of the chain; an event changed outside the system, a
/// link swapped or an event taken from the middle fails pointing at the first that does not hold; and while the last verdict is a failure nothing is added but a verification.
/// Each test has a database of its own, because the failing ones leave the chain broken.
/// </summary>
[Trait("Category", "Database")]
public sealed class CustodyVerifierTests
{
    private const string Untrusted = "ALTER TABLE public.custody_event DISABLE TRIGGER custody_event_never_changed; ";
    private const string Trusted = "; ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER custody_event_never_changed";
    private const string RefusedByTheBrokenChain = "CE002";

    private static readonly DateTimeOffset Moment = new(2031, 3, 4, 5, 6, 7, TimeSpan.Zero);

    [DatabaseFact]
    public async Task WholeChainPassesDeclaringScopeCountAndMomentAndTheVerdictIsAnEventOfTheChain()
    {
        await using var database = new CustodyDatabase();
        await AppendEventsAsync(database, 3);

        var verification = await (await VerifierAsync(database)).VerifyAsync(TestContext.Current.CancellationToken);

        var verdict = (await database.EventsAsync())[^1];
        Assert.Equal((true, VerificationScope.Installation, 3L, Moment), (verification.Passed, verification.Scope, verification.EventsRead, verification.VerifiedAt));
        Assert.Null(verification.FirstInconsistent);
        Assert.Equal((CustodyOutcome.Appended, 4L), (verification.Recorded.Outcome, verification.Recorded.Sequence));
        Assert.Equal(("verify_chain", "verification_passed", "custody_chain", 3L, true, null), (verdict.Step, verdict.Reason, verdict.ItemKind, verdict.ItemId, verdict.ActorSystem, verdict.Corrects));
    }

    [DatabaseFact]
    public async Task DigestsOfEventsByAPersonBySystemAndByCorrectionAreTheOnesTheServerWrote()
    {
        await using var database = new CustodyDatabase();
        var chain = await database.ChainAsync();
        var person = await database.PersonAsync(active: true);
        _ = await AppendAsync(chain, CustodyAuthor.Person(person), new CustodyItem(CustodyItemKind.Person, person), CustodyStep.PersonCreated);
        _ = await chain.AppendAsync(
            CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.ItemText, 9), CustodyStep.RawTextRead, CustodyReason.ContentRead), TestContext.Current.CancellationToken);
        _ = await chain.AppendAsync(CustodyEvent.Correction(CustodyAuthor.Person(person), new CustodyItem(CustodyItemKind.Person, person), 1), TestContext.Current.CancellationToken);

        var verification = await (await VerifierAsync(database)).VerifyAsync(TestContext.Current.CancellationToken);

        Assert.True(verification.Passed);
        Assert.Equal(3, verification.EventsRead);
    }

    [DatabaseFact]
    public async Task VerificationOfAnEmptyChainPassesWithZeroEvents()
    {
        await using var database = new CustodyDatabase();

        var verification = await (await VerifierAsync(database)).VerifyAsync(TestContext.Current.CancellationToken);

        Assert.Equal((true, 0L, 1L), (verification.Passed, verification.EventsRead, verification.Recorded.Sequence));
    }

    [DatabaseFact]
    public async Task EventChangedOutsideTheSystemFailsPointingAtExactlyThatEventAndTheFailureIsRecorded()
    {
        await using var database = new CustodyDatabase();
        await AppendEventsAsync(database, 4);
        await database.RunAsSuperuserAsync($"{Untrusted}UPDATE public.custody_event SET item_id = item_id + 1 WHERE sequence = 3{Trusted}");

        var verification = await (await VerifierAsync(database)).VerifyAsync(TestContext.Current.CancellationToken);

        var verdict = (await database.EventsAsync())[^1];
        Assert.Equal((false, new InconsistentEvent(3, ChainInconsistency.DigestMismatch), 4L), (verification.Passed, verification.FirstInconsistent, verification.EventsRead));
        Assert.Equal((5L, "verification_failed", 4L, 3L), (verdict.Sequence, verdict.Reason, verdict.ItemId, verdict.Corrects));
    }

    [DatabaseFact]
    public async Task PreviousLinkSwappedFailsPointingAtThatEvent()
    {
        await using var database = new CustodyDatabase();
        await AppendEventsAsync(database, 4);
        await database.RunAsSuperuserAsync($"{Untrusted}UPDATE public.custody_event SET previous_digest = decode(repeat('ab', 32), 'hex') WHERE sequence = 2{Trusted}");

        var verification = await (await VerifierAsync(database)).VerifyAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new InconsistentEvent(2, ChainInconsistency.PreviousDigestMismatch), verification.FirstInconsistent);
    }

    [DatabaseFact]
    public async Task EventTakenFromTheMiddleFailsPointingAtTheOneThatFollowedIt()
    {
        await using var database = new CustodyDatabase();
        await AppendEventsAsync(database, 4);
        await database.RunAsSuperuserAsync($"{Untrusted}DELETE FROM public.custody_event WHERE sequence = 2{Trusted}");

        var verification = await (await VerifierAsync(database)).VerifyAsync(TestContext.Current.CancellationToken);

        Assert.Equal((new InconsistentEvent(3, ChainInconsistency.SequenceGap), 3L), (verification.FirstInconsistent, verification.EventsRead));
    }

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
            await using var transaction = await Core.Persistence.Content.ContentTransaction.BeginAsync(source, 1, TestContext.Current.CancellationToken);
            await using var command = transaction.CreateCommand("INSERT INTO content.item_text (domain_id, raw_text) VALUES (1, '\\x00')");
            _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        });

        Assert.Equal(RefusedByTheBrokenChain, refused.SqlState);
    }

    [DatabaseFact]
    public async Task VerificationThatPassesAgainLiftsTheRefusal()
    {
        await using var database = new CustodyDatabase();
        await AppendEventsAsync(database, 3);
        await database.RunAsSuperuserAsync($"{Untrusted}UPDATE public.custody_event SET item_id = item_id + 1 WHERE sequence = 2{Trusted}");
        var verifier = await VerifierAsync(database);
        _ = await verifier.VerifyAsync(TestContext.Current.CancellationToken);
        await database.RunAsSuperuserAsync($"{Untrusted}UPDATE public.custody_event SET item_id = item_id - 1 WHERE sequence = 2{Trusted}");

        var passed = await verifier.VerifyAsync(TestContext.Current.CancellationToken);
        var appended = await (await database.ChainAsync()).AppendAsync(Ordinary(), TestContext.Current.CancellationToken);

        Assert.Equal((true, 4L), (passed.Passed, passed.EventsRead));
        Assert.Equal(CustodyOutcome.Appended, appended.Outcome);
    }

    [DatabaseTheory]
    [InlineData("true, 'person', 1, 'verify_chain', 'verification_passed'")]
    [InlineData("true, 'custody_chain', 1, 'person_created', 'verification_passed'")]
    [InlineData("true, 'custody_chain', 1, 'verify_chain', 'administrative_act'")]
    [InlineData("true, 'custody_chain', 1, 'person_created', 'administrative_act'")]
    public async Task VerdictIsOnlyEverAboutTheChainWrittenByTheSystemAndWithAVerdict(string values)
    {
        await using var database = new CustodyDatabase();

        var refused = await database.RefusedToAppAsync($"INSERT INTO public.custody_event (actor_system, item_kind, item_id, step, reason) VALUES ({values})");

        Assert.Equal(PostgresErrorCodes.CheckViolation, refused.SqlState);
        Assert.Equal("custody_event_verification_shape", refused.ConstraintName);
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
