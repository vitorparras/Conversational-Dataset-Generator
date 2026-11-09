using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Core.Persistence.Tests.ServerGuarantees;

namespace Core.Persistence.Tests.People;

/// <summary>The names of use of several people at once, against a real server: how a list says who submitted without one reading per line, and without authenticating anybody.</summary>
[Trait("Category", "Database")]
public sealed class PersonRegistryNamesTests : IClassFixture<PeopleDatabase>
{
    private static readonly string Annotation = PersonFunctions.Accepted[0];

    private readonly PeopleDatabase database;

    public PersonRegistryNamesTests(PeopleDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task EachIdentifierGivenGivesTheNameOfUseOfItsPersonActiveOrNot()
    {
        var registry = await database.RegistryAsync();
        var (first, second) = (PeopleDatabase.NewName(), PeopleDatabase.NewName());
        var one = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(first, Annotation), CustodyAuthor.System, TestContext.Current.CancellationToken));
        var other = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(second, Annotation), CustodyAuthor.System, TestContext.Current.CancellationToken));
        _ = await registry.DeactivateAsync(other, CustodyAuthor.System, TestContext.Current.CancellationToken);

        var names = await registry.NamesOfAsync(new[] { one, other, one }, TestContext.Current.CancellationToken);

        Assert.Equal(2, names.Count);
        Assert.Equal(first, names[one]);
        Assert.Equal(second, names[other]);
    }

    [DatabaseFact]
    public async Task AnIdentifierNobodyHasIsLeftOutAndTheOthersStillCome()
    {
        var registry = await database.RegistryAsync();
        var name = PeopleDatabase.NewName();
        var id = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(name, Annotation), CustodyAuthor.System, TestContext.Current.CancellationToken));

        var names = await registry.NamesOfAsync(new[] { id, long.MaxValue }, TestContext.Current.CancellationToken);

        Assert.Equal(name, Assert.Single(names).Value);
        Assert.False(names.ContainsKey(long.MaxValue));
    }

    [Fact]
    public async Task NoIdentifierGivesNoNameWithoutAskingTheServer()
    {
        await using var server = new UnreachableServer();
        var registry = new PersonRegistry(server.DataSource);

        var names = await registry.NamesOfAsync(Array.Empty<long>(), TestContext.Current.CancellationToken);

        Assert.Empty(names);
    }

    [Fact]
    public async Task NoListIsRefusedBeforeTheServerIsAsked()
    {
        await using var server = new UnreachableServer();
        var registry = new PersonRegistry(server.DataSource);

        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => registry.NamesOfAsync(null!, TestContext.Current.CancellationToken));
    }
}
