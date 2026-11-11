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
/// The decision on a change of configuration (#634, part 3; decision 0053): another person approves or refuses a submission, and the decision is only the event about it, by who decided,
/// pointing at the event of the submission by its sequence; no row is written. The server refuses a decision by the person who submitted, one that points at another event, a second one on
/// the same submission and one after the longest deadline (168 h), whatever the one who writes.
/// </summary>
[Trait("Category", "Database")]
public sealed class ConfigurationDecisionTests : IClassFixture<PeopleDatabase>
{
    private const string Decision = """
        SELECT concat_ws('|', step, reason, actor_person_id, item_id, reference) FROM public.custody_event
        WHERE item_kind = 'configuration_change' AND step IN ('configuration_approved', 'configuration_refused') AND sequence = @sequence
        """;

    private const string Rows = "SELECT count(*) FROM public.configuration_change";

    private readonly PeopleDatabase database;

    public ConfigurationDecisionTests(PeopleDatabase database)
    {
        this.database = database;
    }

    [DatabaseTheory]
    [InlineData(true, "configuration_approved")]
    [InlineData(false, "configuration_refused")]
    public async Task Ca3ADecisionByAnotherPersonIsAnEventPointingAtTheSubmissionAndNoRow(bool approved, string step)
    {
        var (changes, change) = await SubmittedAsync();
        var decider = await NewPersonAsync();
        var rows = await OwnerScalarAsync(Rows);

        var recorded = await changes.DecideAsync(decider, change, approved, TestContext.Current.CancellationToken);

        Assert.Equal(CustodyOutcome.Appended, recorded.Outcome);
        Assert.Equal($"{step}|administrative_act|{decider}|{change.Id}|{change.Sequence}", await DecisionAsync(recorded.Sequence!.Value));
        Assert.Equal(rows, await OwnerScalarAsync(Rows));
        Assert.True((await changes.SubmittedAsync(change.Id, TestContext.Current.CancellationToken))!.Decided);
    }

    [DatabaseFact]
    public async Task ASubmissionIsReadAsItWasSubmittedAndPending()
    {
        var (_, change) = await SubmittedAsync();

        Assert.Equal(("Session:IdleTimeout", "00:20:00", "operational_adjustment", false), (change.Key, change.Value, change.Motive, change.Decided));
        Assert.InRange(change.SubmittedAt, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(5));
    }

    [DatabaseFact]
    public async Task ASubmissionThatDoesNotExistIsReadAsNone()
    {
        var changes = new ConfigurationChanges(await database.ApplicationAsync());

        Assert.Null(await changes.SubmittedAsync(9_000_000_017, TestContext.Current.CancellationToken));
    }

    [DatabaseFact]
    public async Task ASecondDecisionOnTheSameSubmissionIsAConflict()
    {
        var (changes, change) = await SubmittedAsync();
        _ = await changes.DecideAsync(await NewPersonAsync(), change, approved: false, TestContext.Current.CancellationToken);

        var second = await changes.DecideAsync(await NewPersonAsync(), change, approved: true, TestContext.Current.CancellationToken);

        Assert.Equal(CustodyOutcome.Conflict, second.Outcome);
    }

    [DatabaseFact]
    public async Task Ca2TheServerRefusesTheDecisionOfWhoSubmitted()
    {
        var (_, change) = await SubmittedAsync();

        var refused = await database.RefusedToAppAsync(DecisionBy(change.AuthorId, change.Id, change.Sequence));

        Assert.Equal((PostgresErrorCodes.CheckViolation, "configuration_decision_by_another"), (refused.SqlState, refused.ConstraintName));
    }

    [DatabaseFact]
    public async Task TheServerRefusesADecisionThatPointsAtAnotherEvent()
    {
        var (_, change) = await SubmittedAsync();

        var refused = await database.RefusedToAppAsync(DecisionBy(await NewPersonAsync(), change.Id, change.Sequence - 1));

        Assert.Equal((PostgresErrorCodes.CheckViolation, "configuration_decision_points_at_submission"), (refused.SqlState, refused.ConstraintName));
    }

    [DatabaseFact]
    public async Task TheServerRefusesADecisionAfterTheLongestDeadline()
    {
        var submitter = await NewPersonAsync();
        var id = await OwnerScalarAsync(
            $"""
            WITH c AS (INSERT INTO public.configuration_change (config_key, new_value, motive, submitted_by, submitted_at)
                       VALUES ('Session:IdleTimeout', '00:20:00', 'operational_adjustment', {submitter}, clock_timestamp() - interval '169 hours') RETURNING id)
            INSERT INTO public.custody_event (actor_person_id, item_kind, item_id, step, reason) SELECT {submitter}, 'configuration_change', id, 'configuration_submitted', 'administrative_act' FROM c
            RETURNING item_id
            """);
        var change = (await new ConfigurationChanges(await database.ApplicationAsync()).SubmittedAsync(id, TestContext.Current.CancellationToken))!;

        var refused = await database.RefusedToAppAsync(DecisionBy(await NewPersonAsync(), change.Id, change.Sequence));

        Assert.Equal((PostgresErrorCodes.CheckViolation, "configuration_decision_in_time"), (refused.SqlState, refused.ConstraintName));
    }

    private static string DecisionBy(long person, long changeId, long sequence) =>
        $"""
        INSERT INTO public.custody_event (actor_person_id, item_kind, item_id, step, reason, reference)
        VALUES ({person}, 'configuration_change', {changeId}, 'configuration_approved', 'administrative_act', '{sequence}')
        """;

    private async Task<(ConfigurationChanges Changes, SubmittedChange Change)> SubmittedAsync()
    {
        var changes = new ConfigurationChanges(await database.ApplicationAsync());
        var submission = new ConfigurationSubmission { Key = "Session:IdleTimeout", Value = "00:20:00", Motive = "operational_adjustment", AuthorId = await NewPersonAsync() };
        var written = await changes.SubmitAsync(submission, TestContext.Current.CancellationToken);
        return (changes, (await changes.SubmittedAsync(written.ChangeId!.Value, TestContext.Current.CancellationToken))!);
    }

    private async Task<long> NewPersonAsync()
    {
        var registry = await database.RegistryAsync();
        return PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), PersonFunctions.Administration), CustodyAuthor.System, TestContext.Current.CancellationToken));
    }

    private async Task<string> DecisionAsync(long sequence)
    {
        await using var connection = await Sql.OpenAsync((await database.ScratchAsync()).OwnerConnectionString);
        await using var command = new NpgsqlCommand(Decision, connection);
        _ = command.Parameters.AddWithValue("sequence", sequence);
        return (string)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    [SuppressMessage("Security", "CA2100", Justification = "Each statement is built from literals of this class and numbers the database gave, never from input.")]
    private async Task<long> OwnerScalarAsync(string statement)
    {
        await using var connection = await Sql.OpenAsync((await database.ScratchAsync()).OwnerConnectionString);
        await using var command = new NpgsqlCommand(statement, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture);
    }
}
