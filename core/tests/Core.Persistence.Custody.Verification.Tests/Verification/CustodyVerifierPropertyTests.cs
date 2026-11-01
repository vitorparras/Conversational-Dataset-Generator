using System.Globalization;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Verification;
using Core.Persistence.Tests.Custody;
using Core.Persistence.Tests.Fixtures;
using Core.Tests.Support.Harness;
using CsCheck;

namespace Core.Persistence.Tests.Verification;

/// <summary>
/// The invariant of the verification (requisito 020, decisão 0007, #61 CA-4): whatever chain is written and whichever event is changed from outside the system, in whichever field the digest
/// covers, the verification points at exactly that event; and put back as it was, the chain verifies again. The change is made by the superuser with the trigger that forbids it
/// disabled for the one statement, which only a test does. The property runs against a real server, so its cases are fewer than the declared ones.
/// </summary>
[Trait("Category", "Database")]
public sealed class CustodyVerifierPropertyTests : IClassFixture<CustodyDatabase>
{
    private static readonly Lock Turn = new();
    private const int FieldsOfAnEvent = 8;
    private const int FieldsOfAVerdict = 5;

    // A failure recorded closes the chain to a verdict that passes until a restoration, which brings the state of a copy without it; each case ends the way a restoration does.
    private const string ForgetTheFailure =
        "ALTER TABLE public.custody_event DISABLE TRIGGER custody_event_never_changed; "
        + "DELETE FROM public.custody_event WHERE step = 'verify_chain' AND reason = 'verification_failed'; "
        + "ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER custody_event_never_changed";

    private static readonly PropertyBudget Budget = new(Cases: 15, Deadline: TimeSpan.FromMinutes(4));

    private static readonly Gen<CasePlan> Plans = Gen.Select(
        Gen.Select(Gen.Bool, Gen.Int[0, 2], Gen.Int[1, 1_000], (byPerson, shape, item) => new PlannedEvent(byPerson, shape, item)).List[1, 6],
        Gen.Int[0, 1_000],
        Gen.Int[0, FieldsOfAnEvent - 1],
        Gen.Int[1, 50],
        (events, target, field, delta) => new CasePlan(events, target, field, delta));

    private readonly CustodyDatabase database;

    public CustodyVerifierPropertyTests(CustodyDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    [Trait("Kind", "Property")]
    public async Task EventChangedFromOutsideIsExactlyTheOneTheVerificationPointsAtWhateverTheChainAndTheField()
    {
        var person = await database.PersonAsync(active: true);
        var verifier = new CustodyVerifier(await database.DataSourceAsync(), TimeProvider.System);
        var chain = await database.ChainAsync();
        var runner = new PropertyRunner(Environment.GetEnvironmentVariable, Budget, TestContext.Current.SendDiagnosticMessage);

        runner.Check(Plans, plan => OneAfterAnother(() => HoldsAsync(plan, person, chain, verifier)));
    }

    // The runner samples on several threads, and the chain is one: a case writes, changes and verifies it, so another case at the same time would meet its changes and its links.
    private static void OneAfterAnother(Func<Task> held)
    {
        lock (Turn)
        {
            held().GetAwaiter().GetResult();
        }
    }

    private async Task HoldsAsync(CasePlan plan, long person, CustodyChain chain, CustodyVerifier verifier)
    {
        await WriteAsync(plan, person, chain);
        var before = await verifier.VerifyAsync(TestContext.Current.CancellationToken);
        Assert.True(before.Passed, $"A chain written only through the application must verify; it pointed at {before.FirstInconsistent}.");

        var rows = await database.EventsAsync();
        var target = rows[plan.Target % rows.Count];
        var field = plan.Field % (target.Step == "verify_chain" ? FieldsOfAVerdict : FieldsOfAnEvent);
        // The verdict written next links to the digest the tip holds at that moment, so a digest changed at the tip could not be put back and verify again.
        var tampered = Tampering.Of(target, target == rows[^1] && field == 4 ? 3 : field, plan.Delta);
        await database.RunAsSuperuserAsync(tampered.Change);
        ChainVerification after;
        try
        {
            after = await verifier.VerifyAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            await database.RunAsSuperuserAsync(tampered.Restore);
            await database.RunAsSuperuserAsync(ForgetTheFailure);
        }

        var restored = await verifier.VerifyAsync(TestContext.Current.CancellationToken);

        Assert.False(after.Passed, $"Changing {tampered.Field} of the event {target.Sequence} went unnoticed.");
        Assert.True(
            after.FirstInconsistent!.Sequence == target.Sequence,
            $"Changing {tampered.Field} of the event {target.Sequence} was pointed at {after.FirstInconsistent}, and not at the event changed.");
        Assert.True(restored.Passed, $"The chain put back as it was must verify; it pointed at {restored.FirstInconsistent}.");
    }

    private static async Task WriteAsync(CasePlan plan, long person, CustodyChain chain)
    {
        long? first = null;
        foreach (var planned in plan.Events)
        {
            var author = planned.ByPerson ? CustodyAuthor.Person(person) : CustodyAuthor.System;
            var custodyEvent = planned.Shape switch
            {
                1 => CustodyEvent.Of(author, new CustodyItem(CustodyItemKind.ItemText, planned.Item), CustodyStep.RawTextRead, CustodyReason.ContentRead),
                2 when first is not null => CustodyEvent.Correction(author, new CustodyItem(CustodyItemKind.Person, planned.Item), first.Value),
                _ => CustodyEvent.Of(author, new CustodyItem(CustodyItemKind.Person, planned.Item), CustodyStep.PersonCreated, CustodyReason.AdministrativeAct),
            };
            var appended = await chain.AppendAsync(custodyEvent, TestContext.Current.CancellationToken);
            Assert.Equal(CustodyOutcome.Appended, appended.Outcome);
            first ??= appended.Sequence;
        }
    }

    private sealed record PlannedEvent(bool ByPerson, int Shape, long Item);

    private sealed record CasePlan(IReadOnlyList<PlannedEvent> Events, int Target, int Field, int Delta);

    /// <summary>One change of one field of one event, the statement that makes it and the one that puts the event back as it was.</summary>
    private sealed record Tampering(string Field, string Change, string Restore)
    {
        private const string Untrusted = "ALTER TABLE public.custody_event DISABLE TRIGGER custody_event_never_changed; ";
        private const string Trusted = "; ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER custody_event_never_changed";

        internal static Tampering Of(EventRow row, int field, int delta)
        {
            var (name, set) = Set(row, field, delta);
            return new Tampering(
                name,
                $"{Untrusted}UPDATE public.custody_event SET {set} WHERE sequence = {row.Sequence}{Trusted}",
                $"{Untrusted}UPDATE public.custody_event SET {AsItWas(row)} WHERE sequence = {row.Sequence}{Trusted}");
        }

        // The first five fields are open to every event; a verdict may not change its step, its reason or what it corrects without breaking the shape the database requires of it.
        private static (string Name, string Set) Set(EventRow row, int field, int delta) => field switch
        {
            0 => ("item_id", $"item_id = item_id + {delta}"),
            1 => ("recorded_by", $"recorded_by = recorded_by || 'x{delta}'"),
            2 => ("occurred_at", $"occurred_at = occurred_at + {delta} * interval '1 microsecond'"),
            3 => ("previous_digest", $"previous_digest = pg_catalog.decode(pg_catalog.repeat('{delta + 15:x2}', 32), 'hex')"),
            4 => ("digest", $"digest = pg_catalog.decode(pg_catalog.repeat('{delta + 100:x2}', 32), 'hex')"),
            5 => ("step", $"step = CASE WHEN step = 'person_created' THEN 'person_deactivated' ELSE 'person_created' END"),
            6 => ("reason", $"reason = CASE WHEN reason = 'administrative_act' THEN 'correction_of_record' ELSE 'administrative_act' END"),
            _ => ("corrects", $"corrects = CASE WHEN corrects IS NULL THEN {row.Sequence} ELSE NULL END"),
        };

        private static string AsItWas(EventRow row) => string.Join(
            ", ",
            $"occurred_at = '{row.OccurredAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)}'",
            $"actor_person_id = {Number(row.ActorPersonId)}",
            $"item_id = {row.ItemId}",
            $"step = '{row.Step}'",
            $"reason = '{row.Reason}'",
            $"corrects = {Number(row.Corrects)}",
            $"previous_digest = pg_catalog.decode('{Convert.ToHexStringLower(row.PreviousDigest)}', 'hex')",
            $"digest = pg_catalog.decode('{Convert.ToHexStringLower(row.Digest)}', 'hex')",
            $"recorded_by = '{row.RecordedBy}'");

        private static string Number(long? value) => value is { } present ? present.ToString(CultureInfo.InvariantCulture) : "NULL";
    }
}
