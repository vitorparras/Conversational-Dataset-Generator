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
/// The desk of the changes of configuration over a database (#634, part 2b; decision 0053): an active person with the technical administration writes the submission with its event, and a
/// person of another function or deactivated is refused without anything written.
/// </summary>
[Trait("Category", "Database")]
public sealed class ConfigurationChangeDeskSubmitTests
{
    private const string Key = "Session:IdleTimeout";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [DatabaseFact]
    public async Task TheTechnicalAdministrationWritesTheSubmissionWithItsEvent()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        await using var source = NpgsqlDataSource.Create(database.AppConnectionString);
        var person = await NewPersonAsync(source, PersonFunctions.Administration);

        var answer = await Desk(source).SubmitAsync(Request(person), Token);

        Assert.Equal(ChangeOutcome.Recorded, answer.Outcome);
        Assert.Equal(
            1L,
            await CountAsync(
                database,
                "SELECT count(*) FROM public.custody_event WHERE item_kind = 'configuration_change' AND item_id = @id AND sequence = @sequence AND actor_person_id = @person",
                ("id", answer.ChangeId!.Value),
                ("sequence", answer.Sequence!.Value),
                ("person", person)));
    }

    [DatabaseFact]
    public async Task APersonOfAnotherFunctionOrDeactivatedIsRefusedAndNothingIsWritten()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        await using var source = NpgsqlDataSource.Create(database.AppConnectionString);
        var audit = await NewPersonAsync(source, PersonFunctions.Audit);
        var deactivated = await NewPersonAsync(source, PersonFunctions.Administration);
        _ = await new PersonRegistry(source).DeactivateAsync(deactivated, CustodyAuthor.System, Token);

        var answers = new[] { await Desk(source).SubmitAsync(Request(audit), Token), await Desk(source).SubmitAsync(Request(deactivated), Token) };

        Assert.All(answers, answer => Assert.Equal(ChangeOutcome.WithoutFunction, answer.Outcome));
        Assert.Equal(0L, await CountAsync(database, "SELECT count(*) FROM public.configuration_change"));
    }

    private static ChangeRequest Request(long person) => new(Key, "00:20:00", "operational_adjustment", person);

    private static ConfigurationChangeDesk Desk(NpgsqlDataSource source) => new(InForce(), new ConfigurationChanges(source), new PersonRegistry(source));

    /// <summary>The configuration of an installation the start takes: the arguments the tests of the host start it with, and a database that is never reached through it.</summary>
    private static ConfigurationLoadResult InForce()
    {
        var arguments = new HostInstallation(string.Empty, string.Empty, ["--Recognizer:Kind=fake"]) { ProvidesLanguageService = false }.LaunchArguments
            .Concat(["--Database:Host=127.0.0.1:5432", "--Database:Name=zqx", "--Database:Username=zqx_app", "--Database:Password=ZQX-SENHA-6343"])
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
