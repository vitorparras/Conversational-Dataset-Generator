using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Persistence.Settings;
using Core.Persistence.Tests.Fixtures;
using Core.Persistence.Tests.People;
using Npgsql;

namespace Core.Persistence.Tests.Settings;

/// <summary>
/// The approved change that will never be applied (issue 748; decision 0053): the core records it as an event of the system with the code of the reason and the approval it points at, once;
/// the change leaves the pending ones and the screen reads it with the reason in code; and the server refuses the record of a change not approved, one that points at another event, one already
/// in force and one signed by a person, whatever the one who writes.
/// </summary>
[Trait("Category", "Database")]
public sealed class RefusedChangesTests : IClassFixture<PeopleDatabase>
{
    private const string Recorded = """
        SELECT concat_ws('|', step, reason, actor_system, item_id, reference) FROM public.custody_event
        WHERE item_kind = 'configuration_change' AND step = 'configuration_not_applied' AND sequence = @sequence
        """;

    private readonly PeopleDatabase database;

    public RefusedChangesTests(PeopleDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task TheRefusalIsAnEventOfTheSystemWithTheCodeAndTheApprovalTheChangeLeavesThePendingAndTheScreenReadsIt()
    {
        var (register, found) = await PendingAsync();

        var recorded = await register.RecordNotAppliedAsync(found, "same_author", TestContext.Current.CancellationToken);

        Assert.Equal(CustodyOutcome.Appended, recorded.Outcome);
        Assert.Equal($"configuration_not_applied|administrative_act|true|{found.Id}|same_author/{found.ApprovalSequence}", await EventAsync(recorded.Sequence!.Value));
        Assert.DoesNotContain(await register.PendingAsync(TestContext.Current.CancellationToken), pending => pending.Id == found.Id);
        var read = Assert.Single(await register.RefusedAsync(TestContext.Current.CancellationToken), refused => refused.Id == found.Id);
        Assert.Equal(("Session:IdleTimeout", "same_author", found.ApprovalSequence), (read.Key, read.ReasonCode, read.ApprovalSequence));
    }

    [DatabaseFact]
    public async Task AChangeIsRefusedOnce()
    {
        var (register, found) = await PendingAsync();
        _ = await register.RecordNotAppliedAsync(found, "value_refused", TestContext.Current.CancellationToken);

        var second = await register.RecordNotAppliedAsync(found, "value_refused", TestContext.Current.CancellationToken);

        Assert.Equal(CustodyOutcome.Conflict, second.Outcome);
    }

    [DatabaseFact]
    public async Task TheServerRefusesTheRecordOfAChangeNotApprovedOrPointingAtAnotherEvent()
    {
        var changes = new ConfigurationChanges(await database.ApplicationAsync());
        var notApproved = await SubmittedAsync(changes);
        var (_, found) = await PendingAsync();

        var first = await database.RefusedToAppAsync(RecordBy(notApproved.Id, notApproved.Sequence));
        var second = await database.RefusedToAppAsync(RecordBy(found.Id, found.ApprovalSequence - 1));

        Assert.Equal(
            ((PostgresErrorCodes.CheckViolation, "configuration_not_applied_points_at_approval"), (PostgresErrorCodes.CheckViolation, "configuration_not_applied_points_at_approval")),
            ((first.SqlState, first.ConstraintName), (second.SqlState, second.ConstraintName)));
    }

    [DatabaseFact]
    public async Task TheServerRefusesTheRecordOfAChangeAlreadyInForce()
    {
        var (register, found) = await PendingAsync();
        _ = await register.RecordInForceAsync(found, TestContext.Current.CancellationToken);

        var refused = await database.RefusedToAppAsync(RecordBy(found.Id, found.ApprovalSequence));

        Assert.Equal((PostgresErrorCodes.CheckViolation, "configuration_not_applied_after_in_force"), (refused.SqlState, refused.ConstraintName));
    }

    [DatabaseFact]
    public async Task TheServerRefusesTheRecordSignedByAPerson()
    {
        var (_, found) = await PendingAsync();
        var person = await NewPersonAsync();

        var refused = await database.RefusedToAppAsync(
            $"""
            INSERT INTO public.custody_event (actor_person_id, item_kind, item_id, step, reason, reference)
            VALUES ({person}, 'configuration_change', {found.Id}, 'configuration_not_applied', 'administrative_act', 'same_author/{found.ApprovalSequence}')
            """);

        Assert.Equal((PostgresErrorCodes.CheckViolation, "custody_event_configuration_shape"), (refused.SqlState, refused.ConstraintName));
    }

    private static string RecordBy(long changeId, long sequence) =>
        $"""
        INSERT INTO public.custody_event (actor_system, item_kind, item_id, step, reason, reference)
        VALUES (true, 'configuration_change', {changeId}, 'configuration_not_applied', 'administrative_act', 'same_author/{sequence}')
        """;

    private async Task<SubmittedChange> SubmittedAsync(ConfigurationChanges changes)
    {
        var submission = new ConfigurationSubmission { Key = "Session:IdleTimeout", Value = "00:20:00", Motive = "operational_adjustment", AuthorId = await NewPersonAsync() };
        var written = await changes.SubmitAsync(submission, TestContext.Current.CancellationToken);
        return (await changes.SubmittedAsync(written.ChangeId!.Value, TestContext.Current.CancellationToken))!;
    }

    private async Task<(ApprovedChanges Register, ApprovedChange Change)> PendingAsync()
    {
        var changes = new ConfigurationChanges(await database.ApplicationAsync());
        var submitted = await SubmittedAsync(changes);
        _ = await changes.DecideAsync(await NewPersonAsync(), submitted, approved: true, TestContext.Current.CancellationToken);
        var register = new ApprovedChanges(await database.ApplicationAsync());
        return (register, Assert.Single(await register.PendingAsync(TestContext.Current.CancellationToken), pending => pending.Id == submitted.Id));
    }

    private async Task<long> NewPersonAsync()
    {
        var registry = await database.RegistryAsync();
        return PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), PersonFunctions.Administration), CustodyAuthor.System, TestContext.Current.CancellationToken));
    }

    private async Task<string> EventAsync(long sequence)
    {
        await using var connection = await Sql.OpenAsync((await database.ScratchAsync()).OwnerConnectionString);
        await using var command = new NpgsqlCommand(Recorded, connection);
        _ = command.Parameters.AddWithValue("sequence", sequence);
        return (string)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}
