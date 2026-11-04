using System.Globalization;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Custody;

/// <summary>
/// The migration 0022 (issue 71, part 2): the server takes the steps sign_in_accepted, sign_in_refused and credential_recomputed with the shape that fixes who writes them and about what,
/// keeps every step the migrations before it listed, still takes them while the chain failed its last verification (nobody could sign in to verify otherwise),
/// and refuses the table of attempts any change.
/// </summary>
[Trait("Category", "Database")]
public sealed class CustodySignInListTests : IClassFixture<CustodyDatabase>
{
    private const string Insert = "INSERT INTO public.custody_event (actor_person_id, actor_system, item_kind, item_id, step, reason, corrects, reference) VALUES (";

    private readonly CustodyDatabase database;

    public CustodySignInListTests(CustodyDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task TheThreeStepsAreTakenWithTheAuthorTheirShapeAsks()
    {
        var person = await database.PersonAsync(active: true);

        await database.RunAsOwnerAsync(Row($"{person}, false, 'sign_in_attempt', 16001, 'sign_in_accepted', 'credentials_accepted', NULL, NULL"));
        await database.RunAsOwnerAsync(Row($"{person}, false, 'sign_in_attempt', 16002, 'sign_in_refused', 'password_wrong', NULL, NULL"));
        await database.RunAsOwnerAsync(Row("NULL, true, 'sign_in_attempt', 16003, 'sign_in_refused', 'account_not_recognized', NULL, NULL"));
        await database.RunAsOwnerAsync(Row($"{person}, false, 'person', {person}, 'credential_recomputed', 'administrative_act', NULL, NULL"));

        Assert.Equal(
            4L,
            await database.CountAsOwnerAsync(
                "SELECT count(*) FROM public.custody_event WHERE step IN ('sign_in_accepted', 'sign_in_refused', 'credential_recomputed') "
                + "AND (item_id IN (16001, 16002, 16003) OR item_kind = 'person')"));
    }

    [DatabaseTheory]
    [InlineData("NULL, true, 'sign_in_attempt', 16011, 'sign_in_accepted', 'credentials_accepted', NULL, NULL")]
    [InlineData("{p}, false, 'person', 16012, 'sign_in_accepted', 'credentials_accepted', NULL, NULL")]
    [InlineData("{p}, false, 'sign_in_attempt', 16013, 'sign_in_accepted', 'administrative_act', NULL, NULL")]
    [InlineData("NULL, true, 'sign_in_attempt', 16014, 'sign_in_refused', 'password_wrong', NULL, NULL")]
    [InlineData("{p}, false, 'sign_in_attempt', 16015, 'sign_in_refused', 'account_not_recognized', NULL, NULL")]
    [InlineData("{p}, false, 'sign_in_attempt', 16016, 'sign_in_refused', 'credentials_accepted', NULL, NULL")]
    [InlineData("{p}, false, 'sign_in_attempt', 16017, 'sign_in_refused', 'administrative_act', NULL, NULL")]
    [InlineData("{p}, false, 'custody_chain', 16018, 'sign_in_refused', 'password_wrong', NULL, NULL")]
    [InlineData("{p}, false, 'person', 16019, 'person_created', 'password_wrong', NULL, NULL")]
    [InlineData("{p}, false, 'sign_in_attempt', 16020, 'person_created', 'administrative_act', NULL, NULL")]
    [InlineData("NULL, true, 'person', {p}, 'credential_recomputed', 'administrative_act', NULL, NULL")]
    [InlineData("{q}, false, 'person', {p}, 'credential_recomputed', 'administrative_act', NULL, NULL")]
    [InlineData("{p}, false, 'person', {p}, 'credential_recomputed', 'content_read', NULL, NULL")]
    [InlineData("{p}, false, 'sign_in_attempt', {p}, 'credential_recomputed', 'administrative_act', NULL, NULL")]
    public Task AnEventOfTheEntryWithAnotherShapeIsRefusedByTheShapeCheck(string values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return ShapeIsRefusedAsync(values);
    }

    private async Task ShapeIsRefusedAsync(string values)
    {
        var person = Convert.ToString(await database.PersonAsync(active: true), CultureInfo.InvariantCulture);
        var other = Convert.ToString(await database.PersonAsync(active: true), CultureInfo.InvariantCulture);

        var refused = await database.RefusedToOwnerAsync(Row(values.Replace("{p}", person, StringComparison.Ordinal).Replace("{q}", other, StringComparison.Ordinal)));

        Assert.Equal((PostgresErrorCodes.CheckViolation, "custody_event_sign_in_shape"), (refused.SqlState, refused.ConstraintName));
    }

    [DatabaseFact]
    public async Task AnEventOfTheEntryWithAReferenceIsRefusedByTheCheckOfTheReference()
    {
        var person = await database.PersonAsync(active: true);

        var refused = await database.RefusedToOwnerAsync(Row($"{person}, false, 'sign_in_attempt', 16031, 'sign_in_accepted', 'credentials_accepted', NULL, 'zqx-1'"));

        Assert.Equal((PostgresErrorCodes.CheckViolation, "custody_event_emptying_shape"), (refused.SqlState, refused.ConstraintName));
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
    public async Task EveryStepTheMigrationsBeforeListedIsStillListed(string step)
    {
        var definition = await database.TextAsOwnerAsync("SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'custody_event_step_listed'");

        Assert.Contains($"'{step}'::text", definition, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task AChainThatFailedItsLastVerificationStillTakesTheEntryAndRefusesWhatIsNotAnEntryOrAReading()
    {
        await using var own = new CustodyDatabase();
        var person = await own.PersonAsync(active: true);
        await own.RunAsOwnerAsync(Row("NULL, true, 'custody_chain', 0, 'verify_chain', 'verification_failed', NULL, NULL"));

        await own.RunAsOwnerAsync(Row($"{person}, false, 'sign_in_attempt', 16041, 'sign_in_accepted', 'credentials_accepted', NULL, NULL"));
        await own.RunAsOwnerAsync(Row("NULL, true, 'sign_in_attempt', 16042, 'sign_in_refused', 'account_not_recognized', NULL, NULL"));
        await own.RunAsOwnerAsync(Row($"{person}, false, 'person', {person}, 'credential_recomputed', 'administrative_act', NULL, NULL"));
        var refused = await own.RefusedToOwnerAsync(Row("NULL, true, 'person', 16043, 'person_created', 'administrative_act', NULL, NULL"));

        Assert.Equal("CE002", refused.SqlState);
    }

    [DatabaseFact]
    public async Task ARowOfTheTableOfAttemptsIsNeverChangedNorRemovedNorEmptiedNotEvenByWhoGetsThePrivilegeBack()
    {
        await using var own = new CustodyDatabase();
        await own.RunAsOwnerAsync("INSERT INTO public.sign_in_attempt (network) VALUES ('203.0.113.0/24')");
        const string Update = "UPDATE public.sign_in_attempt SET network = '198.51.100.0/24'";
        const string Delete = "DELETE FROM public.sign_in_attempt";
        const string Truncate = "TRUNCATE public.sign_in_attempt";

        PostgresException[] withoutPrivilege =
            [await own.RefusedToOwnerAsync(Update), await own.RefusedToOwnerAsync(Delete), await own.RefusedToOwnerAsync(Truncate), await own.RefusedToAppAsync(Update)];
        await own.RunAsOwnerAsync("GRANT UPDATE, DELETE, TRUNCATE ON public.sign_in_attempt TO CURRENT_USER");
        PostgresException[] byTheTriggers = [await own.RefusedToOwnerAsync(Update), await own.RefusedToOwnerAsync(Delete), await own.RefusedToOwnerAsync(Truncate)];

        Assert.All(withoutPrivilege, refusal => Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refusal.SqlState));
        Assert.All(byTheTriggers, refusal => Assert.Equal("CE001", refusal.SqlState));
        Assert.Equal(1L, await own.CountAsOwnerAsync("SELECT count(*) FROM public.sign_in_attempt"));
    }

    [DatabaseTheory]
    [InlineData("203.0.113.0/32")]
    [InlineData("203.0.0.0/16")]
    [InlineData("2001:db8:1:2::/64")]
    [InlineData("2001:db8::/32")]
    public async Task OnlyAPrefixOfTwentyFourBitsOrOfFortyEightIsKept(string network)
    {
        var refused = await database.RefusedToAppAsync($"INSERT INTO public.sign_in_attempt (network) VALUES ('{network}')");

        Assert.Equal((PostgresErrorCodes.CheckViolation, "sign_in_attempt_network_prefix"), (refused.SqlState, refused.ConstraintName));
    }

    [DatabaseFact]
    public async Task TheApplicationAddsAnAttemptWithTheNetworkAloneAndTheServerSaysWhenAndWhich()
    {
        var refused = await database.RefusedToAppAsync("INSERT INTO public.sign_in_attempt (network, occurred_at) VALUES ('203.0.113.0/24', now())");

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
    }

    private static string Row(string values) => Insert + values + ")";
}
