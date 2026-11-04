using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Custody;

/// <summary>
/// The migration 0020 (issue 62, CA-6): the server takes the step seal_recorded about the chain, written by the system or by a person for an administrative act, keeps every step the migrations
/// before it listed, and keeps refusing every event of the new step with another shape.
/// </summary>
[Trait("Category", "Database")]
public sealed class CustodySealListTests : IClassFixture<CustodyDatabase>
{
    private const string Insert = "INSERT INTO public.custody_event (actor_person_id, actor_system, item_kind, item_id, step, reason, corrects, reference) VALUES (";

    private readonly CustodyDatabase database;

    public CustodySealListTests(CustodyDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task Ca6TheSealRecordedBySystemAndByAPersonIsTakenFromTheApplicationRoleAndFromTheOwner()
    {
        var person = await database.PersonAsync(active: true);

        await database.RunAsOwnerAsync(Row("NULL, true, 'custody_chain', 15001, 'seal_recorded', 'administrative_act', NULL, NULL"));
        await database.RunAsOwnerAsync(Row($"{person}, false, 'custody_chain', 15002, 'seal_recorded', 'administrative_act', NULL, NULL"));

        Assert.Equal(2L, await database.CountAsOwnerAsync("SELECT count(*) FROM public.custody_event WHERE step = 'seal_recorded' AND item_id IN (15001, 15002)"));
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
    public async Task Ca6EveryStepTheMigrationsBeforeListedIsStillListed(string step)
    {
        var definition = await database.TextAsOwnerAsync("SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'custody_event_step_listed'");

        Assert.Contains($"'{step}'::text", definition, StringComparison.Ordinal);
    }

    [DatabaseTheory]
    [InlineData("NULL, true, 'person', 15003, 'seal_recorded', 'administrative_act', NULL, NULL", "custody_event_verification_shape")]
    [InlineData("NULL, true, 'item_text', 15004, 'seal_recorded', 'administrative_act', NULL, NULL", "custody_event_verification_shape")]
    [InlineData("NULL, true, 'custody_chain', 15005, 'seal_recorded', 'verification_passed', NULL, NULL", "custody_event_seal_shape")]
    [InlineData("NULL, true, 'custody_chain', 15006, 'seal_recorded', 'content_read', NULL, NULL", "custody_event_seal_shape")]
    [InlineData("NULL, true, 'custody_chain', 15007, 'seal_recorded', 'administrative_act', NULL, 'zqx-1'", "custody_event_emptying_shape")]
    public async Task Ca6TheSealHasTheShapeOfTheChainWithAnAdministrativeActAndNoReferenceAndNoOther(string values, string constraint)
    {
        var refused = await database.RefusedToOwnerAsync(Row(values));

        Assert.Equal((PostgresErrorCodes.CheckViolation, constraint), (refused.SqlState, refused.ConstraintName));
    }

    [DatabaseFact]
    public async Task Ca6TheSealThatCorrectsAnEventIsRefused()
    {
        await using var own = new CustodyDatabase();
        await own.RunAsOwnerAsync(Row("NULL, true, 'person', 15008, 'person_created', 'administrative_act', NULL, NULL"));

        var refused = await own.RefusedToOwnerAsync(Row("NULL, true, 'custody_chain', 15009, 'seal_recorded', 'administrative_act', 1, NULL"));

        Assert.Equal((PostgresErrorCodes.CheckViolation, "custody_event_seal_shape"), (refused.SqlState, refused.ConstraintName));
    }

    [DatabaseFact]
    public async Task Ca6AStepOutsideTheListIsStillRefused()
    {
        var refused = await database.RefusedToOwnerAsync(Row("NULL, true, 'custody_chain', 15010, 'seal_record', 'administrative_act', NULL, NULL"));

        Assert.Equal((PostgresErrorCodes.CheckViolation, "custody_event_step_listed"), (refused.SqlState, refused.ConstraintName));
    }

    private static string Row(string values) => Insert + values + ")";
}
