using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Custody;

/// <summary>
/// The migration 0036 (issue 597): the server takes the step password_changed about a person who is also the author, for an administrative act and with the sessions it ended, in digits, as the
/// reference; it keeps every step the migrations before listed, and refuses every event of the new step with another shape.
/// </summary>
[Trait("Category", "Database")]
public sealed class CustodyPasswordChangedListTests : IClassFixture<CustodyDatabase>
{
    private const string Insert = "INSERT INTO public.custody_event (actor_person_id, actor_system, item_kind, item_id, step, reason, corrects, reference) VALUES (";

    private readonly CustodyDatabase database;

    public CustodyPasswordChangedListTests(CustodyDatabase database)
    {
        this.database = database;
    }

    [DatabaseTheory]
    [InlineData("0")]
    [InlineData("3")]
    [InlineData("999999")]
    public async Task TheStepIsTakenWhenThePersonIsTheAuthorAndTheReferenceIsTheCount(string reference)
    {
        var person = await database.PersonAsync(active: true);

        await database.RunAsOwnerAsync(Row($"{person}, false, 'person', {person}, 'password_changed', 'administrative_act', NULL, '{reference}'"));

        var count = await database.CountAsOwnerAsync($"SELECT count(*) FROM public.custody_event WHERE step = 'password_changed' AND item_id = {person} AND reference = '{reference}'");
        Assert.Equal(1L, count);
    }

    [DatabaseTheory]
    [InlineData("person_created")]
    [InlineData("person_password_set")]
    [InlineData("credential_recomputed")]
    [InlineData("domain_rewrapped")]
    [InlineData("password_changed")]
    public async Task EveryStepTheMigrationsBeforeListedIsStillListed(string step)
    {
        var definition = await database.TextAsOwnerAsync("SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'custody_event_step_listed'");

        Assert.Contains($"'{step}'::text", definition, StringComparison.Ordinal);
    }

    [DatabaseTheory]
    [InlineData("author is another person")]
    [InlineData("author is the system")]
    [InlineData("item is not a person")]
    [InlineData("reason is not the administrative act")]
    public async Task AnEventOfTheStepWithAnotherShapeIsRefused(string shape)
    {
        var person = await database.PersonAsync(active: true);
        var other = await database.PersonAsync(active: true);
        var values = shape switch
        {
            "author is another person" => $"{other}, false, 'person', {person}, 'password_changed', 'administrative_act', NULL, '1'",
            "author is the system" => $"NULL, true, 'person', {person}, 'password_changed', 'administrative_act', NULL, '1'",
            "reason is not the administrative act" => $"{person}, false, 'person', {person}, 'password_changed', 'content_read', NULL, '1'",
            _ => $"{person}, false, 'domain', {person}, 'password_changed', 'administrative_act', NULL, '1'",
        };

        var refused = await database.RefusedToOwnerAsync(Row(values));

        Assert.Equal(PostgresErrorCodes.CheckViolation, refused.SqlState);
    }

    [DatabaseTheory]
    [InlineData("NULL")]
    [InlineData("'v1'")]
    [InlineData("'-1'")]
    [InlineData("'1000000'")]
    [InlineData("''")]
    [InlineData("'1 2'")]
    public async Task TheReferenceIsOnlyTheCountInDigits(string reference)
    {
        var person = await database.PersonAsync(active: true);

        var refused = await database.RefusedToOwnerAsync(Row($"{person}, false, 'person', {person}, 'password_changed', 'administrative_act', NULL, {reference}"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, refused.SqlState);
    }

    private static string Row(string values) => Insert + values + ")";
}
