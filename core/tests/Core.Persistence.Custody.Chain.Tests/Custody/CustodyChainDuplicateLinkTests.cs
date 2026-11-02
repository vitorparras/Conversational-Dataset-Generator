using System.Data;
using Core.Persistence.Custody;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Custody;

/// <summary>The server alone refuses a second successor of a link, whatever the code and the isolation of the writers (#58, CA-3).</summary>
[Trait("Category", "Database")]
public sealed class CustodyChainDuplicateLinkTests : IClassFixture<CustodyDatabase>
{
    private const string DisableTrigger = "ALTER TABLE public.custody_event DISABLE TRIGGER custody_event_link; ";
    private const string Columns = "(sequence, actor_system, item_kind, item_id, step, reason, previous_digest, digest)";
    private const string Fixed = "true, 'person', 9002, 'person_created', 'administrative_act'";
    private const string RepeatedPreviousDigest =
        $"{DisableTrigger}INSERT INTO public.custody_event {Columns} SELECT 900000, {Fixed}, previous_digest, decode(repeat('cd', 32), 'hex') FROM public.custody_event WHERE sequence = 1";

    private readonly CustodyDatabase database;

    public CustodyChainDuplicateLinkTests(CustodyDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task InsertThatRepeatsThePreviousDigestOfAnEventIsRefusedByTheServer()
    {
        await AppendOneAsync(9001);

        var refused = await database.RefusedToOwnerAsync(RepeatedPreviousDigest);

        Assert.Equal((PostgresErrorCodes.UniqueViolation, "custody_event_previous_digest_key"), (refused.SqlState, refused.ConstraintName));
    }

    [DatabaseFact]
    public async Task InsertThatRepeatsTheSequenceOfAnEventIsRefusedByTheServer()
    {
        await AppendOneAsync(9101);

        var refused = await database.RefusedToOwnerAsync(
            $"{DisableTrigger}INSERT INTO public.custody_event {Columns} " +
            $"SELECT sequence, {Fixed}, decode(repeat('ef', 32), 'hex'), decode(repeat('ab', 32), 'hex') FROM public.custody_event WHERE sequence = 1");

        Assert.Equal((PostgresErrorCodes.UniqueViolation, "custody_event_pkey"), (refused.SqlState, refused.ConstraintName));
    }

    [DatabaseFact]
    public async Task RefusedInsertLeavesTheLinkingTriggerEnabled()
    {
        await AppendOneAsync(9201);
        _ = await database.RefusedToOwnerAsync(RepeatedPreviousDigest);

        var mode = await database.TextAsOwnerAsync("SELECT tgenabled::text FROM pg_trigger WHERE tgrelid = 'public.custody_event'::regclass AND tgname = 'custody_event_link'");

        Assert.Equal("O", mode);
    }

    [DatabaseFact]
    public async Task SecondWriterOfAWeakerIsolationBehindAnOpenOneIsRefusedWhenTheFirstCommits()
    {
        await using var held = await database.HoldAsync(9301, IsolationLevel.ReadCommitted);
        var second = database.RefusedToAppAsync(Insert(9302));
        Assert.True(await database.WaitForABlockedWriterAsync());

        await held.CommitAsync();
        var refused = await second;

        Assert.Equal(PostgresErrorCodes.UniqueViolation, refused.SqlState);
        Assert.Equal([9301L], (await database.EventsAsync()).Select(row => row.ItemId).Where(item => item is 9301 or 9302));
        ChainIntegrity.Holds(await database.EventsAsync());
    }

    private static string Insert(long item) =>
        $"INSERT INTO public.custody_event (actor_system, item_kind, item_id, step, reason) VALUES (true, 'person', {item}, 'person_created', 'administrative_act')";

    private async Task AppendOneAsync(long item)
    {
        var chain = await database.ChainAsync();
        var custodyEvent = CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.Person, item), CustodyStep.PersonCreated, CustodyReason.AdministrativeAct);
        _ = await chain.AppendAsync(custodyEvent, TestContext.Current.CancellationToken);
    }
}
