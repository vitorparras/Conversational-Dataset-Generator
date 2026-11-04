using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Custody;

/// <summary>
/// The migration 0023 (issue 67, CA-5): the server takes the step key_loss_declared about the master key, written by the system for an administrative act, with what was tried in the id and the scope
/// in the reference; it keeps every step the migrations before it listed, and refuses every event of the new step with another shape.
/// </summary>
[Trait("Category", "Database")]
public sealed class CustodyKeyLossListTests : IClassFixture<CustodyDatabase>
{
    private const string Insert = "INSERT INTO public.custody_event (actor_person_id, actor_system, item_kind, item_id, step, reason, corrects, reference) VALUES (";

    private readonly CustodyDatabase database;

    public CustodyKeyLossListTests(CustodyDatabase database)
    {
        this.database = database;
    }

    [DatabaseTheory]
    [InlineData("master")]
    [InlineData("texto-bruto/1/v1")]
    [InlineData("mapa-reversao/12/v3")]
    public async Task Ca5TheStepIsTakenWithTheShapeOfTheLossForTheWholeKeyAndForALabel(string scope)
    {
        await database.RunAsOwnerAsync(Row($"NULL, true, 'master_key', 7, 'key_loss_declared', 'administrative_act', NULL, '{scope}'"));

        Assert.Equal(1L, await database.CountAsOwnerAsync($"SELECT count(*) FROM public.custody_event WHERE step = 'key_loss_declared' AND reference = '{scope}'"));
    }

    [DatabaseTheory]
    [InlineData("person_created")]
    [InlineData("person_deactivated")]
    [InlineData("person_function_changed")]
    [InlineData("person_password_set")]
    [InlineData("correction")]
    [InlineData("raw_text_read")]
    [InlineData("sanitized_text_read")]
    [InlineData("reversal_entries_read")]
    [InlineData("verify_chain")]
    [InlineData("item_emptied")]
    [InlineData("item_emptying_refused")]
    [InlineData("epoch_rotated")]
    [InlineData("domain_created")]
    [InlineData("seal_recorded")]
    [InlineData("domain_label_rotated")]
    [InlineData("key_loss_declared")]
    [InlineData("recovery_tested")]
    public async Task Ca5EveryStepTheMigrationsBeforeListedIsStillListed(string step)
    {
        var definition = await database.TextAsOwnerAsync("SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'custody_event_step_listed'");

        Assert.Contains($"'{step}'::text", definition, StringComparison.Ordinal);
    }

    [DatabaseTheory]
    [InlineData("person")]
    [InlineData("item_text")]
    [InlineData("reversal_map")]
    [InlineData("custody_chain")]
    [InlineData("content_epoch")]
    [InlineData("domain")]
    [InlineData("master_key")]
    public async Task Ca5EveryKindOfItemTheMigrationsBeforeListedIsStillListed(string kind)
    {
        var definition = await database.TextAsOwnerAsync("SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'custody_event_item_kind_listed'");

        Assert.Contains($"'{kind}'::text", definition, StringComparison.Ordinal);
    }

    [DatabaseTheory]
    [InlineData("domain_label_rotated", "domain", "'v2'")]
    [InlineData("item_emptied", "item_text", "'zqx-1'")]
    [InlineData("seal_recorded", "custody_chain", "NULL")]
    public async Task Ca5TheClausesOfTheEarlierStepsHoldAsTheyDid(string step, string kind, string reference)
    {
        var refused = await database.RefusedToOwnerAsync(Row($"NULL, true, '{kind}', 17001, '{step}', 'content_read', NULL, {reference}"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, refused.SqlState);
    }

    [DatabaseTheory]
    [InlineData("NULL, true, 'person', 17003, 'key_loss_declared', 'administrative_act', NULL, 'master'")]
    [InlineData("NULL, true, 'master_key', 17004, 'key_loss_declared', 'content_read', NULL, 'master'")]
    [InlineData("NULL, true, 'master_key', 17005, 'key_loss_declared', 'administrative_act', NULL, NULL")]
    [InlineData("NULL, true, 'master_key', 17006, 'key_loss_declared', 'administrative_act', NULL, 'v2'")]
    [InlineData("NULL, true, 'master_key', 17007, 'key_loss_declared', 'administrative_act', NULL, 'texto-bruto/0/v1'")]
    [InlineData("NULL, true, 'master_key', 17008, 'key_loss_declared', 'administrative_act', NULL, 'segredo/1/v1'")]
    [InlineData("NULL, true, 'master_key', 17009, 'key_loss_declared', 'administrative_act', NULL, 'master/1/v1'")]
    [InlineData("NULL, true, 'master_key', 0, 'key_loss_declared', 'administrative_act', NULL, 'master'")]
    [InlineData("NULL, true, 'master_key', 8, 'key_loss_declared', 'administrative_act', NULL, 'master'")]
    [InlineData("NULL, true, 'master_key', 17010, 'verify_chain', 'verification_passed', NULL, NULL")]
    public async Task Ca5TheNewStepHasTheShapeOfTheLossAndNoOther(string values)
    {
        var refused = await database.RefusedToOwnerAsync(Row(values));

        Assert.Equal(PostgresErrorCodes.CheckViolation, refused.SqlState);
    }

    [DatabaseFact]
    public async Task Ca5ALossWrittenByAPersonIsRefused()
    {
        var person = await database.PersonAsync(active: true);

        var refused = await database.RefusedToOwnerAsync(Row($"{person}, false, 'master_key', 3, 'key_loss_declared', 'administrative_act', NULL, 'master'"));

        Assert.Equal((PostgresErrorCodes.CheckViolation, "custody_event_key_loss_shape"), (refused.SqlState, refused.ConstraintName));
    }

    private static string Row(string values) => Insert + values + ")";
}
