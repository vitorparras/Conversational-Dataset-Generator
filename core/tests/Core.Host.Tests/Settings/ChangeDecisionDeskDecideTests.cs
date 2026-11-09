using System.Diagnostics.CodeAnalysis;
using Core.Host.Settings;
using Core.Persistence.Custody;
using Core.Persistence.Migrations;
using Core.Persistence.People;
using Core.Persistence.Settings;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Configuration;
using Npgsql;

namespace Core.Host.Tests.Settings;

/// <summary>
/// The desk of the decisions over a database (#634, part 3; decision 0053): an active person of the technical administration other than the one who proposed decides, and the decision is
/// the event about the submission; a person of another function or deactivated is refused without anything written; and a second decision on the same submission is refused.
/// </summary>
[Trait("Category", "Database")]
public sealed class ChangeDecisionDeskDecideTests
{
    private const string Key = "Session:IdleTimeout";
    private const string Decisions = "SELECT count(*) FROM public.custody_event WHERE item_kind = 'configuration_change' AND step IN ('configuration_approved', 'configuration_refused')";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [DatabaseFact]
    public async Task AnActiveAdministratorDecidesAndASecondDecisionIsRefused()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        await using var source = NpgsqlDataSource.Create(database.AppConnectionString);
        var change = await ProposedAsync(source);
        var desk = Desk(source);

        var first = await desk.DecideAsync(new DecisionRequest(change, Approved: true, await NewPersonAsync(source, PersonFunctions.Administration)), Token);
        var second = await desk.DecideAsync(new DecisionRequest(change, Approved: false, await NewPersonAsync(source, PersonFunctions.Administration)), Token);

        Assert.Equal((DecisionOutcome.Decided, DecisionOutcome.AlreadyDecided), (first, second));
        Assert.Equal(
            1L,
            await CountAsync(
                database,
                "SELECT count(*) FROM public.custody_event WHERE item_kind = 'configuration_change' AND item_id = @id AND step = 'configuration_approved'",
                ("id", change)));
    }

    [DatabaseFact]
    public async Task APersonOfAnotherFunctionOrDeactivatedIsRefusedAndNothingIsWritten()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        await using var source = NpgsqlDataSource.Create(database.AppConnectionString);
        var change = await ProposedAsync(source);
        var audit = await NewPersonAsync(source, PersonFunctions.Audit);
        var deactivated = await NewPersonAsync(source, PersonFunctions.Administration);
        _ = await new PersonRegistry(source).DeactivateAsync(deactivated, CustodyAuthor.System, Token);

        var outcomes = new[]
        {
            await Desk(source).DecideAsync(new DecisionRequest(change, Approved: true, audit), Token),
            await Desk(source).DecideAsync(new DecisionRequest(change, Approved: true, deactivated), Token),
        };

        Assert.All(outcomes, outcome => Assert.Equal(DecisionOutcome.WithoutFunction, outcome));
        Assert.Equal(0L, await CountAsync(database, Decisions));
    }

    private static ChangeDecisionDesk Desk(NpgsqlDataSource source) =>
        new(InForce(), new ConfigurationChanges(source), new PersonRegistry(source), TimeProvider.System);

    /// <summary>A change of the session limit proposed by an administrator of its own, as the screen writes it.</summary>
    private static async Task<long> ProposedAsync(NpgsqlDataSource source)
    {
        var author = await NewPersonAsync(source, PersonFunctions.Administration);
        var submission = new ConfigurationSubmission { Key = Key, Value = "00:20:00", Motive = "operational_adjustment", AuthorId = author };
        return (await new ConfigurationChanges(source).SubmitAsync(submission, Token)).ChangeId!.Value;
    }

    /// <summary>The configuration of an installation the start takes, with the deadline by default.</summary>
    private static ConfigurationLoadResult InForce()
    {
        var arguments = new HostInstallation(string.Empty, string.Empty, ["--Recognizer:Kind=fake"]) { ProvidesLanguageService = false }.LaunchArguments
            .Concat(["--Database:Host=127.0.0.1:5432", "--Database:Name=zqx", "--Database:Username=zqx_app", "--Database:Password=ZQX-SENHA-6345"])
            .ToArray();
        var values = arguments.Select(argument => argument[2..].Split('=', 2)).ToDictionary(pair => pair[0], pair => pair[1], StringComparer.Ordinal);
        return ClosedConfiguration.Load(HostConfiguration.Schema(migrating: false, comparing: false), new TrialSource(values, Key, "00:30:00"));
    }

    private static async Task<long> NewPersonAsync(NpgsqlDataSource source, string function)
    {
        var made = await new PersonRegistry(source).CreateAsync(new NewPerson($"zqx-pessoa-{Guid.NewGuid():N}", function), CustodyAuthor.System, Token);
        Assert.Equal(PersonOutcome.Done, made.Outcome);
        return made.PersonId!.Value;
    }

    [SuppressMessage("Security", "CA2100", Justification = "Each query is a literal of this class, with its values as parameters.")]
    private static async Task<long> CountAsync(ScratchDatabase database, string query, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(database.OwnerConnectionString);
        await connection.OpenAsync(Token);
        await using var command = new NpgsqlCommand(query, connection);
        foreach (var (name, value) in parameters)
        {
            _ = command.Parameters.AddWithValue(name, value);
        }

        return (long)(await command.ExecuteScalarAsync(Token))!;
    }
}
