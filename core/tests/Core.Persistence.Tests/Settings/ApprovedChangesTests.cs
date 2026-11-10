using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Persistence.Settings;
using Core.Persistence.Tests.Fixtures;
using Core.Persistence.Tests.People;
using Npgsql;

namespace Core.Persistence.Tests.Settings;

/// <summary>
/// The approved changes of configuration and their being in force (#634, part 4; decision 0053): the applier reads the changes approved and not yet in force with the authors and the two
/// sequences; the core marks one as in force with an event of the system that points at the approval, once; and the server refuses the mark of a change not approved, one that points at another
/// event and one signed by a person, whatever the one who writes.
/// </summary>
[Trait("Category", "Database")]
public sealed class ApprovedChangesTests : IClassFixture<PeopleDatabase>
{
    private const string InForce = """
        SELECT concat_ws('|', step, reason, actor_system, item_id, reference) FROM public.custody_event
        WHERE item_kind = 'configuration_change' AND step = 'configuration_in_force' AND sequence = @sequence
        """;

    private readonly PeopleDatabase database;

    public ApprovedChangesTests(PeopleDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task AnApprovedChangeIsReadWithItsAuthorsAndTheSequencesOfTheTwoEvents()
    {
        var (change, submitter, approver, approval) = await ApprovedAsync();

        var found = Assert.Single(await (await RegisterAsync()).PendingAsync(TestContext.Current.CancellationToken), pending => pending.Id == change.Id);

        var expected = ("Session:IdleTimeout", "00:20:00", submitter, change.Sequence, approver, approval);
        Assert.Equal(expected, (found.Key, found.Value, found.SubmitterId, found.SubmissionSequence, found.ApproverId, found.ApprovalSequence));
    }

    [DatabaseFact]
    public async Task AChangeStillPendingOrRefusedIsNotRead()
    {
        var changes = new ConfigurationChanges(await database.ApplicationAsync());
        var pending = await SubmittedAsync(changes);
        var refused = await SubmittedAsync(changes);
        _ = await changes.DecideAsync(await NewPersonAsync(), refused, approved: false, TestContext.Current.CancellationToken);

        var read = await (await RegisterAsync()).PendingAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(read, change => change.Id == pending.Id || change.Id == refused.Id);
    }

    [DatabaseFact]
    public async Task AnApprovalThatALaterOneOfTheSameKeyHasSupersededIsNotReadAndTheLaterOneIs()
    {
        var (older, _, _, _) = await ApprovedAsync();
        var (newer, _, _, _) = await ApprovedAsync();

        var read = await (await RegisterAsync()).PendingAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(read, pending => pending.Id == older.Id);
        Assert.Contains(read, pending => pending.Id == newer.Id);
    }

    [DatabaseFact]
    public async Task TheChangeInForceIsAnEventOfTheSystemThatPointsAtTheApprovalAndIsNotReadAgain()
    {
        var (change, _, _, approval) = await ApprovedAsync();
        var register = await RegisterAsync();
        var found = Assert.Single(await register.PendingAsync(TestContext.Current.CancellationToken), pending => pending.Id == change.Id);

        var recorded = await register.RecordInForceAsync(found, TestContext.Current.CancellationToken);

        Assert.Equal(CustodyOutcome.Appended, recorded.Outcome);
        Assert.Equal($"configuration_in_force|administrative_act|true|{change.Id}|{approval}", await InForceAsync(recorded.Sequence!.Value));
        Assert.DoesNotContain(await register.PendingAsync(TestContext.Current.CancellationToken), pending => pending.Id == change.Id);
    }

    [DatabaseFact]
    public async Task AChangeIsInForceOnce()
    {
        var (change, _, _, _) = await ApprovedAsync();
        var register = await RegisterAsync();
        var found = Assert.Single(await register.PendingAsync(TestContext.Current.CancellationToken), pending => pending.Id == change.Id);
        _ = await register.RecordInForceAsync(found, TestContext.Current.CancellationToken);

        var second = await register.RecordInForceAsync(found, TestContext.Current.CancellationToken);

        Assert.Equal(CustodyOutcome.Conflict, second.Outcome);
    }

    [DatabaseFact]
    public async Task TheServerRefusesTheMarkOfAChangeNotApprovedAndOneThatPointsAtAnotherEvent()
    {
        var changes = new ConfigurationChanges(await database.ApplicationAsync());
        var notApproved = await SubmittedAsync(changes);
        var (approved, _, _, approval) = await ApprovedAsync();

        var first = await database.RefusedToAppAsync(MarkBy(notApproved.Id, notApproved.Sequence));
        var second = await database.RefusedToAppAsync(MarkBy(approved.Id, approval - 1));

        Assert.Equal(
            ((PostgresErrorCodes.CheckViolation, "configuration_in_force_points_at_approval"), (PostgresErrorCodes.CheckViolation, "configuration_in_force_points_at_approval")),
            ((first.SqlState, first.ConstraintName), (second.SqlState, second.ConstraintName)));
    }

    [DatabaseFact]
    public async Task TheServerRefusesTheMarkSignedByAPerson()
    {
        var (change, _, _, approval) = await ApprovedAsync();
        var person = await NewPersonAsync();

        var refused = await database.RefusedToAppAsync(
            $"""
            INSERT INTO public.custody_event (actor_person_id, item_kind, item_id, step, reason, reference)
            VALUES ({person}, 'configuration_change', {change.Id}, 'configuration_in_force', 'administrative_act', '{approval}')
            """);

        Assert.Equal((PostgresErrorCodes.CheckViolation, "custody_event_configuration_shape"), (refused.SqlState, refused.ConstraintName));
    }

    private static string MarkBy(long changeId, long sequence) =>
        $"""
        INSERT INTO public.custody_event (actor_system, item_kind, item_id, step, reason, reference)
        VALUES (true, 'configuration_change', {changeId}, 'configuration_in_force', 'administrative_act', '{sequence}')
        """;

    private static async Task<SubmittedChange> SubmittedAsync(ConfigurationChanges changes, long author)
    {
        var submission = new ConfigurationSubmission { Key = "Session:IdleTimeout", Value = "00:20:00", Motive = "operational_adjustment", AuthorId = author };
        var written = await changes.SubmitAsync(submission, TestContext.Current.CancellationToken);
        return (await changes.SubmittedAsync(written.ChangeId!.Value, TestContext.Current.CancellationToken))!;
    }

    private async Task<SubmittedChange> SubmittedAsync(ConfigurationChanges changes) => await SubmittedAsync(changes, await NewPersonAsync());

    private async Task<(SubmittedChange Change, long Submitter, long Approver, long Approval)> ApprovedAsync()
    {
        var changes = new ConfigurationChanges(await database.ApplicationAsync());
        var change = await SubmittedAsync(changes);
        var approver = await NewPersonAsync();
        var recorded = await changes.DecideAsync(approver, change, approved: true, TestContext.Current.CancellationToken);
        return (change, change.AuthorId, approver, recorded.Sequence!.Value);
    }

    private async Task<ApprovedChanges> RegisterAsync() => new(await database.ApplicationAsync());

    private async Task<long> NewPersonAsync()
    {
        var registry = await database.RegistryAsync();
        return PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), PersonFunctions.Administration), CustodyAuthor.System, TestContext.Current.CancellationToken));
    }

    private async Task<string> InForceAsync(long sequence)
    {
        await using var connection = await Sql.OpenAsync((await database.ScratchAsync()).OwnerConnectionString);
        await using var command = new NpgsqlCommand(InForce, connection);
        _ = command.Parameters.AddWithValue("sequence", sequence);
        return (string)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}
