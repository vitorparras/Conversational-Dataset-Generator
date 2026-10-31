using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.People;

/// <summary>A change reaches the person it names and no other: the rest of the registry stays as it was, in the person and in the record of what was done.</summary>
[Trait("Category", "Database")]
public sealed class PersonChangeScopeTests : IClassFixture<PeopleDatabase>
{
    private static readonly string Annotation = PersonFunctions.Accepted[0];
    private static readonly string Arbitration = PersonFunctions.Accepted[1];

    private readonly PeopleDatabase database;

    public PersonChangeScopeTests(PeopleDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task DeactivatingOnePersonLeavesTheOthersActiveAndTheirRecordUntouched()
    {
        var registry = await database.RegistryAsync();
        var (target, other) = (await NewPersonAsync(registry), await NewPersonAsync(registry));

        _ = PeopleDatabase.Id(await registry.DeactivateAsync(target, CustodyAuthor.System, TestContext.Current.CancellationToken));

        Assert.False((await database.PersonAsync(target))!.Active);
        Assert.True((await database.PersonAsync(other))!.Active);
        _ = Assert.Single(await database.ChangesOfAsync(other));
    }

    [DatabaseFact]
    public async Task ChangingTheFunctionOfOnePersonLeavesTheFunctionOfTheOthers()
    {
        var registry = await database.RegistryAsync();
        var (target, other) = (await NewPersonAsync(registry), await NewPersonAsync(registry));

        _ = PeopleDatabase.Id(await registry.ChangeFunctionAsync(target, Arbitration, CustodyAuthor.System, TestContext.Current.CancellationToken));

        Assert.Equal((Arbitration, Annotation), ((await database.PersonAsync(target))!.Function, (await database.PersonAsync(other))!.Function));
        _ = Assert.Single(await database.ChangesOfAsync(other));
    }

    private static async Task<long> NewPersonAsync(PersonRegistry registry) =>
        PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), Annotation), CustodyAuthor.System, TestContext.Current.CancellationToken));
}
