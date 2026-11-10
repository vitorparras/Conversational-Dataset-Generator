using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Persistence.Settings;
using Core.Persistence.Tests.Fixtures;
using Core.Persistence.Tests.People;
using Npgsql;

namespace Core.Persistence.Tests.Settings;

/// <summary>
/// The register of the changes of configuration (#634, part 2a; decision 0053): a submission is a row and the event configuration_submitted about it, by the person who submitted, written together
/// or not at all; the event carries neither the key nor the value; a row is never changed nor removed, the owner included; and the database takes no row without its event.
/// </summary>
[Trait("Category", "Database")]
public sealed class ConfigurationChangesTests : IClassFixture<PeopleDatabase>
{
    private const string Row = "SELECT concat_ws('|', e.step, e.reason, e.actor_person_id, coalesce(e.reference, '-'), c.config_key, c.new_value, c.motive, c.submitted_by) "
        + "FROM public.custody_event e JOIN public.configuration_change c ON c.id = e.item_id WHERE e.item_kind = 'configuration_change' AND e.sequence = ";

    private readonly PeopleDatabase database;

    public ConfigurationChangesTests(PeopleDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task ASubmissionIsARowAndAnEventByThePersonWithNoReference()
    {
        var person = await NewPersonAsync();

        var result = await (await ChangesAsync()).SubmitAsync(Submission(person), TestContext.Current.CancellationToken);

        Assert.Equal(CustodyOutcome.Appended, result.Recorded.Outcome);
        _ = Assert.NotNull(result.ChangeId);
        Assert.Equal($"configuration_submitted|administrative_act|{person}|-|Session:IdleTimeout|00:20:00|operational_adjustment|{person}", await TextAsync(Row + result.Recorded.Sequence));
    }

    [DatabaseFact]
    public async Task ADeactivatedPersonSubmitsNothingAndLeavesNoRow()
    {
        var registry = await database.RegistryAsync();
        var person = await NewPersonAsync();
        _ = await registry.DeactivateAsync(person, CustodyAuthor.System, TestContext.Current.CancellationToken);
        var rows = await TextAsync("SELECT count(*) FROM public.configuration_change");

        var result = await (await ChangesAsync()).SubmitAsync(Submission(person), TestContext.Current.CancellationToken);

        Assert.Null(result.ChangeId);
        Assert.Equal((CustodyOutcome.ActorDeactivated, rows), (result.Recorded.Outcome, await TextAsync("SELECT count(*) FROM public.configuration_change")));
    }

    [DatabaseFact]
    public async Task APersonNotRegisteredSubmitsNothing()
    {
        var events = await TextAsync("SELECT count(*) FROM public.custody_event");

        var result = await (await ChangesAsync()).SubmitAsync(Submission(9_000_000_017), TestContext.Current.CancellationToken);

        Assert.Equal((CustodyOutcome.ActorUnknown, events), (result.Recorded.Outcome, await TextAsync("SELECT count(*) FROM public.custody_event")));
    }

    [DatabaseTheory]
    [InlineData("SessionIdleTimeout", "00:20:00", "operational_adjustment")]
    [InlineData("Session:IdleTimeout", "", "operational_adjustment")]
    [InlineData("Session:IdleTimeout", "00:20\n", "operational_adjustment")]
    [InlineData("Session:IdleTimeout", "00:20:00", "Ajuste livre")]
    public async Task ASubmissionWithoutTheShapeOfTheDatabaseIsADefectOfTheCaller(string key, string value, string motive)
    {
        var changes = await ChangesAsync();

        _ = await Assert.ThrowsAsync<ArgumentException>(
            () => changes.SubmitAsync(new ConfigurationSubmission { Key = key, Value = value, Motive = motive, AuthorId = 1 }, TestContext.Current.CancellationToken));
    }

    [DatabaseFact]
    public async Task ARowWithoutItsEventIsRefusedWhenTheTransactionEnds()
    {
        var person = await NewPersonAsync();

        var refused = await database.RefusedToAppAsync(
            $"INSERT INTO public.configuration_change (config_key, new_value, motive, submitted_by) VALUES ('Session:IdleTimeout', '00:20:00', 'correction', {person})");

        Assert.Equal("CE005", refused.SqlState);
    }

    [DatabaseTheory]
    [InlineData("UPDATE public.configuration_change SET new_value = '00:05:00'")]
    [InlineData("DELETE FROM public.configuration_change")]
    [InlineData("TRUNCATE public.configuration_change")]
    public async Task ARowIsNeverChangedNorRemovedNotEvenByTheOwner(string statement)
    {
        _ = await (await ChangesAsync()).SubmitAsync(Submission(await NewPersonAsync()), TestContext.Current.CancellationToken);

        var refused = await database.RefusedToOwnerAsync(statement);

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
    }

    [DatabaseTheory]
    [InlineData("UPDATE public.configuration_change SET new_value = '00:05:00'")]
    [InlineData("DELETE FROM public.configuration_change")]
    [InlineData("TRUNCATE public.configuration_change")]
    public async Task AnOwnerWhoGrantsItselfTheRightIsStillRefusedByTheTrigger(string statement)
    {
        _ = await (await ChangesAsync()).SubmitAsync(Submission(await NewPersonAsync()), TestContext.Current.CancellationToken);

        var refused = await database.RefusedToOwnerAsync($"GRANT UPDATE, DELETE, TRUNCATE ON public.configuration_change TO CURRENT_USER; {statement}");

        Assert.Equal("CE001", refused.SqlState);
    }

    private static ConfigurationSubmission Submission(long person) =>
        new() { Key = "Session:IdleTimeout", Value = "00:20:00", Motive = "operational_adjustment", AuthorId = person };

    private async Task<ConfigurationChanges> ChangesAsync() => new(await database.ApplicationAsync());

    private async Task<long> NewPersonAsync()
    {
        var registry = await database.RegistryAsync();
        return PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), PersonFunctions.Administration), CustodyAuthor.System, TestContext.Current.CancellationToken));
    }

    [SuppressMessage("Security", "CA2100", Justification = "Each statement is a literal of this class, never built from input.")]
    private async Task<string> TextAsync(string statement)
    {
        await using var connection = await Sql.OpenAsync((await database.ScratchAsync()).OwnerConnectionString);
        await using var command = new NpgsqlCommand(statement, connection);
        return Convert.ToString(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture)!;
    }
}
