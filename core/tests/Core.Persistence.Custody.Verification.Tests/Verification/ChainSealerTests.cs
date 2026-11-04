using Core.Persistence.Custody;
using Core.Persistence.Custody.Verification;
using Core.Persistence.Tests.Custody;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Verification;

/// <summary>
/// Recording the seal against a server (#62, CA-2): the record is an event of the chain about the chain, with the sequence of the event sealed; the chain shows when it was made, with which
/// count and which digest, and the digest is not repeated in the event.
/// </summary>
[Trait("Category", "Database")]
public sealed class ChainSealerTests
{
    private static readonly DateTimeOffset Moment = new(2031, 3, 4, 5, 6, 7, TimeSpan.Zero);

    [DatabaseFact]
    public async Task RecordingTheSealWritesAnEventOfTheChainThatPointsAtTheEventSealedAndTheChainShowsWhenWhichCountAndWhichDigest()
    {
        await using var database = new CustodyDatabase();
        await AppendEventsAsync(database, 3);

        var recording = await (await SealerAsync(database)).RecordAsync(CustodyAuthor.System, TestContext.Current.CancellationToken);

        var events = await database.EventsAsync();
        var record = events[^1];
        var sealedEvent = events.Single(row => row.Sequence == record.ItemId);
        Assert.Equal((CustodyOutcome.Appended, 4L), (recording.Recorded.Outcome, recording.Recorded.Sequence));
        Assert.Equal(
            ("seal_recorded", "custody_chain", 3L, "administrative_act", true, null),
            (record.Step, record.ItemKind, record.ItemId, record.Reason, record.ActorSystem, record.Corrects));
        Assert.Equal(
            (3L, Moment, Convert.ToHexStringLower(sealedEvent.Digest)),
            (recording.Seal!.Events, recording.Seal.SealedAt, recording.Seal.Digest));
        Assert.Equal(record.ItemId, recording.Seal.Events);
    }

    [DatabaseFact]
    public async Task TheSealIsOfTheLastEventAndNotOfTheRecordItself()
    {
        await using var database = new CustodyDatabase();
        await AppendEventsAsync(database, 2);
        var sealer = await SealerAsync(database);

        var first = await sealer.RecordAsync(CustodyAuthor.System, TestContext.Current.CancellationToken);
        var second = await sealer.RecordAsync(CustodyAuthor.System, TestContext.Current.CancellationToken);

        var events = await database.EventsAsync();
        Assert.Equal((2L, 3L), (first.Seal!.Events, second.Seal!.Events));
        Assert.Equal(Convert.ToHexStringLower(events[2].Digest), second.Seal.Digest);
    }

    [DatabaseFact]
    public async Task ThePersonWhoConfirmedTheRecordIsTheAuthorOfTheEvent()
    {
        await using var database = new CustodyDatabase();
        await AppendEventsAsync(database, 1);
        var person = await database.PersonAsync(active: true);

        var recording = await (await SealerAsync(database)).RecordAsync(CustodyAuthor.Person(person), TestContext.Current.CancellationToken);

        var record = (await database.EventsAsync())[^1];
        Assert.Equal((CustodyOutcome.Appended, person, false), (recording.Recorded.Outcome, record.ActorPersonId, record.ActorSystem));
    }

    [DatabaseFact]
    public async Task APersonDeactivatedCannotConfirmTheRecordAndNothingIsSealed()
    {
        await using var database = new CustodyDatabase();
        await AppendEventsAsync(database, 1);
        var person = await database.PersonAsync(active: false);

        var recording = await (await SealerAsync(database)).RecordAsync(CustodyAuthor.Person(person), TestContext.Current.CancellationToken);

        Assert.Equal((CustodyOutcome.ActorDeactivated, null), (recording.Recorded.Outcome, recording.Seal));
        _ = Assert.Single(await database.EventsAsync());
    }

    [DatabaseFact]
    public async Task ChainWithNoEventsIsSealedAsZeroEventsAtTheOriginOfTheChain()
    {
        await using var database = new CustodyDatabase();

        var recording = await (await SealerAsync(database)).RecordAsync(CustodyAuthor.System, TestContext.Current.CancellationToken);

        Assert.Equal((0L, Convert.ToHexStringLower(new byte[32]), 0L), (recording.Seal!.Events, recording.Seal.Digest, (await database.EventsAsync())[0].ItemId));
    }

    [DatabaseFact]
    public async Task ChainThatFailedItsLastVerificationIsNotSealedAndNothingIsWritten()
    {
        await using var database = new CustodyDatabase();
        await AppendEventsAsync(database, 3);
        await database.RunAsSuperuserAsync(
            "ALTER TABLE public.custody_event DISABLE TRIGGER custody_event_never_changed; UPDATE public.custody_event SET item_id = item_id + 1 WHERE sequence = 2; "
            + "ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER custody_event_never_changed");
        _ = await new CustodyVerifier(await database.DataSourceAsync(), new FixedTime(Moment)).VerifyAsync(TestContext.Current.CancellationToken);

        var recording = await (await SealerAsync(database)).RecordAsync(CustodyAuthor.System, TestContext.Current.CancellationToken);

        Assert.Equal((CustodyOutcome.ChainBroken, null, 4), (recording.Recorded.Outcome, recording.Seal, (await database.EventsAsync()).Count));
    }

    [DatabaseFact]
    public async Task WriterThatReachedTheSameLinkFirstMakesTheRecordAConflictWithNoSealAndNothingWritten()
    {
        await using var database = new CustodyDatabase();
        await AppendEventsAsync(database, 3);
        await database.RunAsSuperuserAsync(
            "CREATE FUNCTION public.zqx_first() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'could not serialize' USING ERRCODE = '40001'; END $$; "
            + "CREATE TRIGGER aaa_zqx_first BEFORE INSERT ON public.custody_event FOR EACH ROW WHEN (NEW.step = 'seal_recorded') EXECUTE FUNCTION public.zqx_first()");

        var recording = await (await SealerAsync(database)).RecordAsync(CustodyAuthor.System, TestContext.Current.CancellationToken);

        Assert.Equal((CustodyOutcome.Conflict, null, 3), (recording.Recorded.Outcome, recording.Seal, (await database.EventsAsync()).Count));
    }

    [DatabaseFact]
    public async Task SealMadeBeforeIsRecordedAsThatSealEvenWhenTheChainGrewAfterItWasMade()
    {
        await using var database = new CustodyDatabase();
        await AppendEventsAsync(database, 3);
        var sealer = await SealerAsync(database);
        var made = await sealer.MakeAsync(TestContext.Current.CancellationToken);
        await AppendEventsAsync(database, 2);

        var recording = await sealer.RecordAsync(made, CustodyAuthor.System, TestContext.Current.CancellationToken);

        var events = await database.EventsAsync();
        Assert.Equal((CustodyOutcome.Appended, 6L, 3L, 3L), (recording.Recorded.Outcome, recording.Recorded.Sequence, events[^1].ItemId, recording.Seal!.Events));
        Assert.Equal(Convert.ToHexStringLower(events.Single(row => row.Sequence == 3).Digest), recording.Seal.Digest);
    }

    [DatabaseFact]
    public async Task MakingTheSealRecordsNothing()
    {
        await using var database = new CustodyDatabase();
        await AppendEventsAsync(database, 3);

        var made = await (await SealerAsync(database)).MakeAsync(TestContext.Current.CancellationToken);

        var events = await database.EventsAsync();
        Assert.Equal((3L, Convert.ToHexStringLower(events[^1].Digest), 3), (made.Events, made.Digest, events.Count));
    }

    [DatabaseTheory]
    [InlineData(3, 1)]
    [InlineData(10, 1)]
    public async Task SealWhoseDigestIsNotTheOneOfTheEventItPointsAtIsRefusedAndNothingIsWritten(long events, byte filler)
    {
        await using var database = new CustodyDatabase();
        await AppendEventsAsync(database, 3);
        var forged = ChainSeal.Of(Moment, events, [.. Enumerable.Repeat(filler, 32)]);

        var recording = await (await SealerAsync(database)).RecordAsync(forged, CustodyAuthor.System, TestContext.Current.CancellationToken);

        Assert.Equal((CustodyOutcome.SealNotOfTheChain, null, 3), (recording.Recorded.Outcome, recording.Seal, (await database.EventsAsync()).Count));
        Assert.False(string.IsNullOrWhiteSpace(recording.Recorded.Refusal));
    }

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

    private static async Task<ChainSealer> SealerAsync(CustodyDatabase database) => new(await database.DataSourceAsync(), new FixedTime(Moment));

    private sealed class FixedTime(DateTimeOffset moment) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => moment;
    }
}
