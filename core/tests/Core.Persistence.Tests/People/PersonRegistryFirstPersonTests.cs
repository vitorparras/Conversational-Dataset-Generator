using Core.Persistence.Connection;
using Core.Persistence.Custody;
using Core.Persistence.Migrations;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.People;

/// <summary>
/// The first person of the installation (issue 65): made by the system only while the registry has nobody, with the check and the write in one statement so that two commands at once make one,
/// and the credential that connects is told apart as the owner's or the application's.
/// </summary>
[Trait("Category", "Database")]
public sealed class PersonRegistryFirstPersonTests
{
    private static readonly string Annotation = PersonFunctions.Accepted[0];

    [DatabaseFact]
    public async Task OnAnEmptyRegistryThePersonIsMadeActiveByTheSystemWithItsRecordOfTheChange()
    {
        await using var database = new PeopleDatabase();
        var registry = await database.RegistryAsync();

        var id = PeopleDatabase.Id(await registry.CreateFirstAsync(new NewPerson(PeopleDatabase.NewName(), Annotation), TestContext.Current.CancellationToken));

        var changes = await database.ChangesOfAsync(id);
        Assert.Equal((true, 1), ((await database.PersonAsync(id))!.Active, changes.Count));
        Assert.Equal(("created", null), (changes[0].Kind, changes[0].ActorId));
    }

    [DatabaseFact]
    public async Task WithSomeoneInTheRegistryTheSecondIsRefusedWithItsOutcomeAndNothingIsWritten()
    {
        await using var database = new PeopleDatabase();
        var registry = await database.RegistryAsync();
        _ = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), Annotation), CustodyAuthor.System, TestContext.Current.CancellationToken));

        var refused = await registry.CreateFirstAsync(new NewPerson(PeopleDatabase.NewName(), Annotation), TestContext.Current.CancellationToken);

        Assert.Equal((PersonOutcome.RegistryNotEmpty, null, 1L, 1L), (refused.Outcome, refused.PersonId, await database.CountAsync("person"), await database.CountAsync("person_change")));
        Assert.Contains("rota com sessão", refused.Refusal, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task TheRegistryIsEmptyUntilSomeoneIsMadeAndStaysNotEmptyWhenTheOnlyPersonIsDeactivated()
    {
        await using var database = new PeopleDatabase();
        var registry = await database.RegistryAsync();
        var before = await registry.IsEmptyAsync(TestContext.Current.CancellationToken);

        var id = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), Annotation), CustodyAuthor.System, TestContext.Current.CancellationToken));
        var made = await registry.IsEmptyAsync(TestContext.Current.CancellationToken);
        _ = await registry.DeactivateAsync(id, CustodyAuthor.System, TestContext.Current.CancellationToken);
        var deactivated = await registry.IsEmptyAsync(TestContext.Current.CancellationToken);

        Assert.Equal((true, false, false), (before, made, deactivated));
    }

    [DatabaseFact]
    public async Task AFunctionOutsideThePolicyIsRefusedBeforeTheRegistryIsLookedAt()
    {
        await using var database = new PeopleDatabase();
        var registry = await database.RegistryAsync();

        var refused = await registry.CreateFirstAsync(new NewPerson(PeopleDatabase.NewName(), "Cozinheiro"), TestContext.Current.CancellationToken);

        Assert.Equal((PersonOutcome.FunctionNotAccepted, 0L), (refused.Outcome, await database.CountAsync("person")));
    }

    [DatabaseFact]
    public async Task CommandsAtOnceMakeOnePersonAndTheOthersAreRefusedWritingNothing()
    {
        await using var database = new PeopleDatabase();
        var registry = await database.RegistryAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => registry.CreateFirstAsync(new NewPerson(PeopleDatabase.NewName(), Annotation), TestContext.Current.CancellationToken)));

        Assert.Equal((1, 1L, 1L), (results.Count(result => result.Outcome == PersonOutcome.Done), await database.CountAsync("person"), await database.CountAsync("person_change")));
        Assert.All(results.Where(result => result.Outcome != PersonOutcome.Done), result => Assert.Contains(result.Outcome, new[] { PersonOutcome.RegistryNotEmpty, PersonOutcome.ChainBusy }));
    }

    [DatabaseFact]
    public async Task TheOwnerOfTheSchemaIsToldFromTheApplicationAndADatabaseNotMigratedHasNoAnswer()
    {
        await using var migrated = await ScratchDatabase.CreateAsync();
        _ = await migrated.MigrateAsync(EmbeddedMigrations.Load());
        await using var bare = await ScratchDatabase.CreateAsync();
        await using var owner = NpgsqlDataSource.Create(migrated.OwnerConnectionString);
        await using var application = NpgsqlDataSource.Create(migrated.AppConnectionString);
        await using var unmigrated = NpgsqlDataSource.Create(bare.AppConnectionString);

        var answers = (
            await OwnerCredentialCheck.HoldsTheSchemaAsync(owner, TestContext.Current.CancellationToken),
            await OwnerCredentialCheck.HoldsTheSchemaAsync(application, TestContext.Current.CancellationToken),
            await OwnerCredentialCheck.HoldsTheSchemaAsync(unmigrated, TestContext.Current.CancellationToken));

        Assert.Equal((true, false, null), answers);
    }
}
