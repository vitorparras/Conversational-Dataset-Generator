using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Core.Persistence.Tests.ServerGuarantees;

namespace Core.Persistence.Tests.People;

/// <summary>The identifier of the person who goes by a name of use, against a real server: it is how the entry finds who asks, and it authenticates nobody.</summary>
[Trait("Category", "Database")]
public sealed class PersonRegistryNameLookupTests : IClassFixture<PeopleDatabase>
{
    private static readonly string Annotation = PersonFunctions.Accepted[0];

    private readonly PeopleDatabase database;

    public PersonRegistryNameLookupTests(PeopleDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task NameOfUseGivesTheIdentifierOfTheActivePersonWhoGoesByIt()
    {
        var registry = await database.RegistryAsync();
        var name = PeopleDatabase.NewName();
        var id = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(name, Annotation), actorId: null, TestContext.Current.CancellationToken));

        var found = await registry.IdOfAsync(name, TestContext.Current.CancellationToken);

        Assert.Equal(id, found);
    }

    [DatabaseFact]
    public async Task NameOfUseOfADeactivatedPersonStillGivesTheIdentifier()
    {
        var registry = await database.RegistryAsync();
        var name = PeopleDatabase.NewName();
        var id = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(name, Annotation), actorId: null, TestContext.Current.CancellationToken));
        _ = await registry.DeactivateAsync(id, actorId: null, TestContext.Current.CancellationToken);

        var found = await registry.IdOfAsync(name, TestContext.Current.CancellationToken);

        Assert.Equal(id, found);
    }

    [DatabaseFact]
    public async Task NameNobodyGoesByGivesNothing()
    {
        var registry = await database.RegistryAsync();

        var found = await registry.IdOfAsync(PeopleDatabase.NewName(), TestContext.Current.CancellationToken);

        Assert.Null(found);
    }

    [DatabaseFact]
    public async Task NameThatDiffersOnlyInCaseIsAnotherNameAndGivesNothing()
    {
        var registry = await database.RegistryAsync();
        var name = PeopleDatabase.NewName();
        _ = await registry.CreateAsync(new NewPerson(name, Annotation), actorId: null, TestContext.Current.CancellationToken);

        var found = await registry.IdOfAsync(name.ToUpperInvariant(), TestContext.Current.CancellationToken);

        Assert.Null(found);
    }

    [DatabaseFact]
    public async Task NameThatIsAStatementIsOnlyANameAndGivesNothing()
    {
        var registry = await database.RegistryAsync();

        var found = await registry.IdOfAsync("x' OR '1'='1", TestContext.Current.CancellationToken);

        Assert.Null(found);
    }

    [Fact]
    public async Task NoNameIsRefusedBeforeTheServerIsAsked()
    {
        await using var server = new UnreachableServer();
        var registry = new PersonRegistry(server.DataSource);

        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => registry.IdOfAsync(null!, TestContext.Current.CancellationToken));
    }
}
