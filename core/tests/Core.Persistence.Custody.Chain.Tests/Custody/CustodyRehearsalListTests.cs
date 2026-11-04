using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Custody;

/// <summary>
/// The migration 0029 (issue 66, CA-2 and CA-3): the server takes the step restore_rehearsed about the chain, written by the system, passed with the mask zero and held, or failed with the mask
/// and the names of what failed; it keeps every step the migrations before it listed, and refuses every event of the new step with another shape.
/// </summary>
[Trait("Category", "Database")]
public sealed class CustodyRehearsalListTests : IClassFixture<CustodyDatabase>
{
    private const string Insert = "INSERT INTO public.custody_event (actor_person_id, actor_system, item_kind, item_id, step, reason, corrects, reference) VALUES (";

    private readonly CustodyDatabase database;

    public CustodyRehearsalListTests(CustodyDatabase database)
    {
        this.database = database;
    }

    [DatabaseTheory]
    [InlineData("custody_chain", 0, "verification_passed", "held")]
    [InlineData("custody_chain", 2, "verification_failed", "policy")]
    [InlineData("custody_chain", 255, "verification_failed", "privilege.policy.revoke.immutable.columns.chain.seal")]
    public async Task Ca2TheStepIsTakenWithTheMaskAndTheNamesOfWhatFailed(string kind, long mask, string reason, string names)
    {
        await database.RunAsOwnerAsync(Row($"NULL, true, '{kind}', {mask}, 'restore_rehearsed', '{reason}', NULL, '{names}'"));

        Assert.Equal(1L, await database.CountAsOwnerAsync($"SELECT count(*) FROM public.custody_event WHERE step = 'restore_rehearsed' AND item_id = {mask} AND reference = '{names}'"));
    }

    [DatabaseTheory]
    [InlineData("verify_chain")]
    [InlineData("seal_recorded")]
    [InlineData("key_loss_declared")]
    [InlineData("verify_guarantees")]
    [InlineData("owner_window_closed")]
    public async Task Ca2EveryStepTheMigrationsBeforeListedIsStillListed(string step)
    {
        var definition = await database.TextAsOwnerAsync("SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'custody_event_step_listed'");

        Assert.Contains($"'{step}'::text", definition, StringComparison.Ordinal);
    }

    [DatabaseTheory]
    [InlineData("NULL, true, 'person', 1, 'restore_rehearsed', 'verification_passed', NULL, 'held'")]
    [InlineData("NULL, false, 'custody_chain', 0, 'restore_rehearsed', 'verification_passed', NULL, 'held'")]
    [InlineData("NULL, true, 'custody_chain', 0, 'restore_rehearsed', 'administrative_act', NULL, 'held'")]
    [InlineData("NULL, true, 'custody_chain', 1, 'restore_rehearsed', 'verification_passed', NULL, 'held'")]
    [InlineData("NULL, true, 'custody_chain', 0, 'restore_rehearsed', 'verification_failed', NULL, 'held'")]
    [InlineData("NULL, true, 'custody_chain', 256, 'restore_rehearsed', 'verification_failed', NULL, 'chain'")]
    [InlineData("NULL, true, 'custody_chain', 1, 'restore_rehearsed', 'verification_failed', NULL, NULL")]
    [InlineData("NULL, true, 'custody_chain', 1, 'restore_rehearsed', 'verification_failed', NULL, 'Privilege'")]
    [InlineData("NULL, true, 'custody_chain', 1, 'restore_rehearsed', 'verification_failed', NULL, 'privilege;chain'")]
    public async Task Ca3TheNewStepHasTheShapeOfTheResultAndNoOther(string values)
    {
        var refused = await database.RefusedToOwnerAsync(Row(values));

        Assert.Equal(PostgresErrorCodes.CheckViolation, refused.SqlState);
    }

    private static string Row(string values) => Insert + values + ")";
}
