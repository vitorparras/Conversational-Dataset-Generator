using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Persistence.Settings;
using Core.Persistence.Tests.Fixtures;
using Core.Persistence.Tests.People;
using Npgsql;

namespace Core.Persistence.Tests.Settings;

/// <summary>
/// The table of the changes of configuration judged by the database with direct statements (#634, part 2a; decision 0053), apart from the register that writes it: what the checks of the key,
/// of the value and of the motive refuse and what they take at their limits, the author who is not a person, the event of the submission that must be by the same person and have the shape
/// the chain fixes, whatever the one who writes.
/// </summary>
[Trait("Category", "Database")]
public sealed class ConfigurationChangeTableTests : IClassFixture<PeopleDatabase>
{
    private const string WithoutEvent = "CE005";
    private const string KeyCheck = "configuration_change_key";
    private const string ValueCheck = "configuration_change_value";
    private const string MotiveCheck = "configuration_change_motive";
    private const string ShapeCheck = "custody_event_configuration_shape";

    private readonly PeopleDatabase database;

    public ConfigurationChangeTableTests(PeopleDatabase database)
    {
        this.database = database;
    }

    [DatabaseTheory]
    [InlineData("'session:IdleTimeout'")]
    [InlineData("'Session:idleTimeout'")]
    [InlineData("'SessionIdleTimeout'")]
    [InlineData("'Session:'")]
    [InlineData("':IdleTimeout'")]
    [InlineData("'Session:Idle1'")]
    [InlineData("'Session:Idle:Timeout'")]
    [InlineData("'Session:IdleTimeout '")]
    [InlineData("E'Session:IdleTimeout\\n'")]
    [InlineData("'S' || repeat('a', 63) || ':Name'")]
    [InlineData("'Section:N' || repeat('a', 63)")]
    [InlineData("''")]
    public async Task AKeyThatIsNotASectionAndANameIsRefusedByTheCheckOfTheKey(string key)
    {
        var person = await NewPersonAsync();

        var refused = await database.RefusedToAppAsync(Insert(key, "'00:20:00'", "'operational_adjustment'", person));

        Assert.Equal((PostgresErrorCodes.CheckViolation, KeyCheck), (refused.SqlState, refused.ConstraintName));
    }

    [DatabaseTheory]
    [InlineData("''")]
    [InlineData("repeat('a', 257)")]
    [InlineData("'café'")]
    [InlineData("E'00:20\\n'")]
    [InlineData("E'a\\tb'")]
    [InlineData("E'a\\x7f'")]
    public async Task AValueEmptyOverTheSizeOrOutOfPrintableAsciiIsRefusedByTheCheckOfTheValue(string value)
    {
        var person = await NewPersonAsync();

        var refused = await database.RefusedToAppAsync(Insert("'Session:IdleTimeout'", value, "'operational_adjustment'", person));

        Assert.Equal((PostgresErrorCodes.CheckViolation, ValueCheck), (refused.SqlState, refused.ConstraintName));
    }

    [DatabaseTheory]
    [InlineData("''")]
    [InlineData("'Ajuste livre'")]
    [InlineData("'Operational'")]
    [InlineData("'1adjustment'")]
    [InlineData("'_adjustment'")]
    [InlineData("'operational-adjustment'")]
    [InlineData("'a' || repeat('b', 32)")]
    [InlineData("E'operational\\n'")]
    public async Task AMotiveThatIsNotACodeInSnakeCaseIsRefusedByTheCheckOfTheMotive(string motive)
    {
        var person = await NewPersonAsync();

        var refused = await database.RefusedToAppAsync(Insert("'Session:IdleTimeout'", "'00:20:00'", motive, person));

        Assert.Equal((PostgresErrorCodes.CheckViolation, MotiveCheck), (refused.SqlState, refused.ConstraintName));
    }

    [DatabaseTheory]
    [InlineData("A", "1", "a")]
    [InlineData("Ab", " ~", "a_b")]
    public async Task TheSmallestKeyValueAndMotiveTheChecksTakeAreRecorded(string section, string value, string motive)
    {
        var person = await NewPersonAsync();

        var result = await (await ChangesAsync()).SubmitAsync(Submission($"{section}:{section}", value, motive, person), TestContext.Current.CancellationToken);

        Assert.Equal(CustodyOutcome.Appended, result.Recorded.Outcome);
    }

    [DatabaseFact]
    public async Task TheLongestKeyValueAndMotiveTheChecksTakeAreRecorded()
    {
        var person = await NewPersonAsync();
        var key = $"S{new string('a', 62)}:N{new string('b', 62)}";

        var result = await (await ChangesAsync()).SubmitAsync(Submission(key, new string('v', 256), $"m{new string('o', 31)}", person), TestContext.Current.CancellationToken);

        Assert.Equal(CustodyOutcome.Appended, result.Recorded.Outcome);
    }

    [DatabaseFact]
    public async Task AnAuthorWhoIsNotAPersonIsRefusedByTheForeignKeyBeforeAnyEvent()
    {
        var refused = await database.RefusedToAppAsync(Insert("'Session:IdleTimeout'", "'00:20:00'", "'operational_adjustment'", 9_000_000_029));

        Assert.Equal((PostgresErrorCodes.ForeignKeyViolation, "configuration_change_author"), (refused.SqlState, refused.ConstraintName));
    }

    [DatabaseFact]
    public async Task TheEventOfAnotherPersonThanTheOneWhoSubmittedIsRefusedWhenTheTransactionEnds()
    {
        var submitter = await NewPersonAsync();
        var another = await NewPersonAsync();

        var refused = await database.RefusedToAppAsync(WithEvent(submitter, another, "configuration_change", "configuration_submitted", "administrative_act", reference: null));

        Assert.Equal(WithoutEvent, refused.SqlState);
    }

    [DatabaseFact]
    [SuppressMessage("Security", "CA2100", Justification = "Each statement is built from literals of this class and numbers the database gave, never from input.")]
    public async Task TheEventOfTheSamePersonInTheSameTransactionIsTheOnlyOneTheDatabaseTakes()
    {
        var person = await NewPersonAsync();
        var before = await ScalarAsync("SELECT count(*) FROM public.configuration_change");

        await using var connection = await Sql.OpenAsync((await database.ScratchAsync()).AppConnectionString);
        await using var command = new NpgsqlCommand(WithEvent(person, person, "configuration_change", "configuration_submitted", "administrative_act", reference: null), connection);
        _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        Assert.Equal(before + 1, await ScalarAsync("SELECT count(*) FROM public.configuration_change"));
    }

    [DatabaseTheory]
    [InlineData("person", "configuration_submitted", "administrative_act", null)]
    [InlineData("configuration_change", "verify_chain", "administrative_act", null)]
    [InlineData("configuration_change", "configuration_submitted", "verification_failed", null)]
    [InlineData("configuration_change", "configuration_submitted", "administrative_act", "invalid_rows")]
    public async Task AnEventOfTheSubmissionWithoutTheShapeTheChainFixesIsRefusedByTheCheck(string kind, string step, string reason, string? reference)
    {
        var person = await NewPersonAsync();

        var refused = await database.RefusedToAppAsync(WithEvent(person, person, kind, step, reason, reference));

        Assert.Equal(PostgresErrorCodes.CheckViolation, refused.SqlState);
    }

    [DatabaseTheory]
    [InlineData("person", "configuration_submitted")]
    [InlineData("configuration_change", "person_created")]
    public async Task TheStepAndTheItemKindOfASubmissionAreOneOrTheOtherNeverOnlyOne(string kind, string step)
    {
        var person = await NewPersonAsync();

        var refused = await database.RefusedToAppAsync(WithEvent(person, person, kind, step, "administrative_act", reference: null));

        Assert.Equal((PostgresErrorCodes.CheckViolation, ShapeCheck), (refused.SqlState, refused.ConstraintName));
    }

    private static string Insert(string key, string value, string motive, long person) =>
        $"INSERT INTO public.configuration_change (config_key, new_value, motive, submitted_by) VALUES ({key}, {value}, {motive}, {person})";

    /// <summary>The row of a person and, in the same statement, the event of another or of the same person, with the kind, the step, the reason and the reference given.</summary>
    private static string WithEvent(long submitter, long author, string kind, string step, string reason, string? reference) =>
        $"WITH c AS ({Insert("'Session:IdleTimeout'", "'00:20:00'", "'operational_adjustment'", submitter)} RETURNING id) "
        + "INSERT INTO public.custody_event (actor_person_id, item_kind, item_id, step, reason, reference) "
        + $"SELECT {author}, '{kind}', id, '{step}', '{reason}', {(reference is null ? "NULL" : $"'{reference}'")} FROM c";

    private static ConfigurationSubmission Submission(string key, string value, string motive, long person) =>
        new() { Key = key, Value = value, Motive = motive, AuthorId = person };

    private async Task<ConfigurationChanges> ChangesAsync() => new(await database.ApplicationAsync());

    private async Task<long> NewPersonAsync()
    {
        var registry = await database.RegistryAsync();
        return PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), PersonFunctions.Administration), CustodyAuthor.System, TestContext.Current.CancellationToken));
    }

    [SuppressMessage("Security", "CA2100", Justification = "Each statement is built from literals of this class and numbers the database gave, never from input.")]
    private async Task<long> ScalarAsync(string statement)
    {
        await using var connection = await Sql.OpenAsync((await database.ScratchAsync()).OwnerConnectionString);
        await using var command = new NpgsqlCommand(statement, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture);
    }
}
