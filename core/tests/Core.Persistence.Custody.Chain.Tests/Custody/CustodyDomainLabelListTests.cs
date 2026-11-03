using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Custody;

/// <summary>
/// The migration 0021 (issue 56, CA-6): the server takes the steps domain_label_rotated and domain_label_revoked about a domain, written by the system for an administrative act and carrying
/// the generation or the label as the reference, keeps every step the migrations before it listed, and refuses every event of the new steps with another shape.
/// </summary>
[Trait("Category", "Database")]
public sealed class CustodyDomainLabelListTests : IClassFixture<CustodyDatabase>
{
    private const string Insert = "INSERT INTO public.custody_event (actor_person_id, actor_system, item_kind, item_id, step, reason, corrects, reference) VALUES (";

    private readonly CustodyDatabase database;

    public CustodyDomainLabelListTests(CustodyDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task Ca6TheTwoStepsAreTakenFromTheOwnerWithTheShapeOfTheDomain()
    {
        await database.RunAsOwnerAsync(Row("NULL, true, 'domain', 16001, 'domain_label_rotated', 'administrative_act', NULL, 'v2'"));
        await database.RunAsOwnerAsync(Row("NULL, true, 'domain', 16002, 'domain_label_revoked', 'administrative_act', NULL, 'texto-bruto/16002/v1'"));
        await database.RunAsOwnerAsync(Row("NULL, true, 'domain', 16003, 'domain_label_revoked', 'administrative_act', NULL, 'mapa-reversao/16003/v12'"));

        const string Count = "SELECT count(*) FROM public.custody_event WHERE step IN ('domain_label_rotated', 'domain_label_revoked') AND item_id BETWEEN 16001 AND 16003";
        Assert.Equal(3L, await database.CountAsOwnerAsync(Count));
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
    [InlineData("domain_label_revoked")]
    public async Task Ca6EveryStepTheMigrationsBeforeListedIsStillListed(string step)
    {
        var definition = await database.TextAsOwnerAsync("SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'custody_event_step_listed'");

        Assert.Contains($"'{step}'::text", definition, StringComparison.Ordinal);
    }

    [DatabaseTheory]
    [InlineData("NULL, true, 'person', 16004, 'domain_label_rotated', 'administrative_act', NULL, 'v2'")]
    [InlineData("NULL, true, 'item_text', 16005, 'domain_label_revoked', 'administrative_act', NULL, 'texto-bruto/1/v1'")]
    [InlineData("NULL, true, 'domain', 16006, 'domain_label_rotated', 'content_read', NULL, 'v2'")]
    [InlineData("NULL, true, 'domain', 16007, 'domain_label_rotated', 'administrative_act', NULL, NULL")]
    [InlineData("NULL, true, 'domain', 16008, 'domain_label_rotated', 'administrative_act', NULL, 'v0'")]
    [InlineData("NULL, true, 'domain', 16009, 'domain_label_rotated', 'administrative_act', NULL, 'v2x'")]
    [InlineData("NULL, true, 'domain', 16010, 'domain_label_rotated', 'administrative_act', NULL, 'texto-bruto/1/v2'")]
    [InlineData("NULL, true, 'domain', 16011, 'domain_label_revoked', 'administrative_act', NULL, NULL")]
    [InlineData("NULL, true, 'domain', 16012, 'domain_label_revoked', 'administrative_act', NULL, 'v2'")]
    [InlineData("NULL, true, 'domain', 16013, 'domain_label_revoked', 'administrative_act', NULL, 'texto-bruto/01/v1'")]
    [InlineData("NULL, true, 'domain', 16014, 'domain_label_revoked', 'administrative_act', NULL, 'outra-coisa/1/v1'")]
    [InlineData("NULL, true, 'domain', 16015, 'domain_created', 'administrative_act', NULL, 'v1'")]
    public async Task Ca6TheNewStepsHaveTheShapeOfTheDomainWithTheReferenceOfTheirOwnAndNoOther(string values)
    {
        var refused = await database.RefusedToOwnerAsync(Row(values));

        Assert.Equal((PostgresErrorCodes.CheckViolation, "custody_event_domain_shape"), (refused.SqlState, refused.ConstraintName));
    }

    [DatabaseFact]
    public async Task Ca6ARotationWrittenByAPersonIsRefused()
    {
        var person = await database.PersonAsync(active: true);

        var refused = await database.RefusedToOwnerAsync(Row($"{person}, false, 'domain', 16016, 'domain_label_rotated', 'administrative_act', NULL, 'v2'"));

        Assert.Equal((PostgresErrorCodes.CheckViolation, "custody_event_domain_shape"), (refused.SqlState, refused.ConstraintName));
    }

    [DatabaseFact]
    public async Task Ca6AReferenceOnAnyOtherStepOfADomainIsStillRefused()
    {
        var refused = await database.RefusedToOwnerAsync(Row("NULL, true, 'custody_chain', 16017, 'seal_recorded', 'administrative_act', NULL, 'v2'"));

        Assert.Equal((PostgresErrorCodes.CheckViolation, "custody_event_emptying_shape"), (refused.SqlState, refused.ConstraintName));
    }

    private static string Row(string values) => Insert + values + ")";
}
