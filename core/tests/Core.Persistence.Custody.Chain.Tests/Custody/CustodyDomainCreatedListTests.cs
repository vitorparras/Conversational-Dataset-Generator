using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Custody;

/// <summary>
/// The migration 0019 (issue 54, CA-4f): the server takes the item kind domain and the step domain_created, written by the system for an administrative act, and keeps refusing every
/// item and step outside the lists and every event of the new step with another shape.
/// </summary>
[Trait("Category", "Database")]
public sealed class CustodyDomainCreatedListTests : IClassFixture<CustodyDatabase>
{
    private const string Insert = "INSERT INTO public.custody_event (actor_person_id, actor_system, item_kind, item_id, step, reason, reference) VALUES (";

    private readonly CustodyDatabase database;

    public CustodyDomainCreatedListTests(CustodyDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task Ca4fTheDomainAndItsCreationAreTakenFromTheApplicationRoleAndFromTheOwner()
    {
        await database.RunAsOwnerAsync(Row("NULL, true, 'domain', 14001, 'domain_created', 'administrative_act', NULL"));
        await database.RunAsOwnerAsync(Row("NULL, true, 'domain', 14002, 'domain_created', 'administrative_act', NULL"));

        Assert.Equal(2L, await database.CountAsOwnerAsync("SELECT count(*) FROM public.custody_event WHERE item_kind = 'domain' AND item_id IN (14001, 14002)"));
    }

    [DatabaseTheory]
    [InlineData("'domains'", "'person_created'", "item_kind_listed")]
    [InlineData("'Domain'", "'person_created'", "item_kind_listed")]
    [InlineData("'person'", "'domain_creat'", "step_listed")]
    [InlineData("'person'", "'domain_created '", "step_listed")]
    public async Task Ca4fAnItemOrAStepOutsideTheListsIsStillRefused(string kind, string step, string constraint)
    {
        var refused = await database.RefusedToOwnerAsync(Row($"NULL, true, {kind}, 14003, {step}, 'administrative_act', NULL"));

        Assert.Equal((PostgresErrorCodes.CheckViolation, "custody_event_" + constraint), (refused.SqlState, refused.ConstraintName));
    }

    [DatabaseTheory]
    [InlineData("NULL, true, 'person', 14004, 'domain_created', 'administrative_act', NULL")]
    [InlineData("NULL, true, 'domain', 14005, 'person_created', 'administrative_act', NULL")]
    [InlineData("NULL, true, 'domain', 14006, 'domain_created', 'correction_of_record', NULL")]
    [InlineData("NULL, true, 'domain', 14007, 'domain_created', 'administrative_act', 'zqx-1'")]
    public async Task Ca4fTheCreationOfADomainHasTheShapeOfTheDomainAndOfTheSystemAndNoOther(string values)
    {
        var refused = await database.RefusedToOwnerAsync(Row(values));

        Assert.Equal((PostgresErrorCodes.CheckViolation, "custody_event_domain_shape"), (refused.SqlState, refused.ConstraintName));
    }

    [DatabaseFact]
    public async Task Ca4fThePersonAsTheAuthorOfTheCreationOfADomainIsRefused()
    {
        var person = await database.PersonAsync(active: true);

        var refused = await database.RefusedToOwnerAsync(Row($"{person}, false, 'domain', 14008, 'domain_created', 'administrative_act', NULL"));

        Assert.Equal((PostgresErrorCodes.CheckViolation, "custody_event_domain_shape"), (refused.SqlState, refused.ConstraintName));
    }

    private static string Row(string values) => Insert + values + ")";
}
