using Core.Host.Settings;
using Core.Persistence.Custody;
using Core.Persistence.Migrations;
using Core.Persistence.People;
using Core.Persistence.Settings;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Core.Host.Tests.Settings;

/// <summary>
/// The record of the changes that will never be applied, over a database (issue 748; decision 0053): a change whose submitter is no longer active is recorded as not applied while its value is not
/// in force, and is not judged at all while the value is in force, since it belongs to the service that marks it in force and the chain must never say both.
/// </summary>
[Trait("Category", "Database")]
public sealed class ConfigurationRefusalsDatabaseTests
{
    private const string Key = "Session:IdleTimeout";
    private const string Asked = "00:20:00";
    private const string Refusals = "SELECT count(*) FROM public.custody_event WHERE item_kind = 'configuration_change' AND step = 'configuration_not_applied'";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [DatabaseFact]
    public async Task AChangeWhoseSubmitterIsNoLongerActiveIsRecordedAsNotAppliedWhileItsValueIsNotInForce()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        await using var source = NpgsqlDataSource.Create(database.AppConnectionString);
        await ApprovedWithSubmitterDeactivatedAsync(source);

        using var loggers = LoggerFactory.Create(_ => { });
        using var service = Service(source, "00:30:00", loggers);
        var recorded = await service.RecordAsync(Token);

        Assert.Equal((1, 1L), (recorded, await CountAsync(database)));
    }

    [DatabaseFact]
    public async Task AChangeWhoseValueIsAlreadyInForceIsNotJudgedAndNothingIsRecorded()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        await using var source = NpgsqlDataSource.Create(database.AppConnectionString);
        await ApprovedWithSubmitterDeactivatedAsync(source);

        using var loggers = LoggerFactory.Create(_ => { });
        using var service = Service(source, Asked, loggers);
        var recorded = await service.RecordAsync(Token);

        Assert.Equal((0, 0L), (recorded, await CountAsync(database)));
    }

    private static ConfigurationRefusals Service(NpgsqlDataSource source, string inForce, ILoggerFactory loggers)
    {
        var facts = new ApplicationFactsReader(source, TimeProvider.System);
        var entries = new[] { new EffectiveEntry(Key, inForce, ConfigurationOrigin.File) };
        return new ConfigurationRefusals(new ApprovedChanges(source), facts, entries, loggers);
    }

    /// <summary>A change of the session limit proposed by an administrator, approved by another, and then the proposer deactivated.</summary>
    private static async Task ApprovedWithSubmitterDeactivatedAsync(NpgsqlDataSource source)
    {
        var author = await NewAdministratorAsync(source);
        var changes = new ConfigurationChanges(source);
        var submission = new ConfigurationSubmission { Key = Key, Value = Asked, Motive = "operational_adjustment", AuthorId = author };
        var id = (await changes.SubmitAsync(submission, Token)).ChangeId!.Value;
        var submitted = (await changes.SubmittedAsync(id, Token))!;
        _ = await changes.DecideAsync(await NewAdministratorAsync(source), submitted, approved: true, Token);
        _ = await new PersonRegistry(source).DeactivateAsync(author, CustodyAuthor.System, Token);
    }

    private static async Task<long> NewAdministratorAsync(NpgsqlDataSource source)
    {
        var made = await new PersonRegistry(source).CreateAsync(new NewPerson($"zqx-pessoa-{Guid.NewGuid():N}", PersonFunctions.Administration), CustodyAuthor.System, Token);
        Assert.Equal(PersonOutcome.Done, made.Outcome);
        return made.PersonId!.Value;
    }

    private static async Task<long> CountAsync(ScratchDatabase database)
    {
        await using var connection = new NpgsqlConnection(database.OwnerConnectionString);
        await connection.OpenAsync(Token);
        await using var command = new NpgsqlCommand(Refusals, connection);
        return (long)(await command.ExecuteScalarAsync(Token))!;
    }
}
