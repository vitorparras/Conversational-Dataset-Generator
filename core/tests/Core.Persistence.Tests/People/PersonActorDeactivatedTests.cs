using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.People;

/// <summary>Whoever was deactivated no longer acts on a person (#109, pendência; #69): each change is refused with the reason and writes neither the person nor the record of the change.</summary>
[Trait("Category", "Database")]
public sealed class PersonActorDeactivatedTests : IClassFixture<PeopleDatabase>
{
    private static readonly string Annotation = PersonFunctions.Accepted[0];
    private static readonly string Arbitration = PersonFunctions.Accepted[1];

    private readonly PeopleDatabase database;

    public PersonActorDeactivatedTests(PeopleDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task DeactivatedActorCannotAddAPerson()
    {
        var registry = await database.RegistryAsync();
        var actor = await DeactivatedActorAsync(registry);
        var (people, changes) = (await database.CountAsync("person"), await database.CountAsync("person_change"));

        var result = await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), Annotation), CustodyAuthor.Person(actor), TestContext.Current.CancellationToken);

        Assert.Equal(PersonOutcome.ActorDeactivated, result.Outcome);
        Assert.NotNull(result.Refusal);
        Assert.Equal((people, changes), (await database.CountAsync("person"), await database.CountAsync("person_change")));
    }

    [DatabaseFact]
    public async Task DeactivatedActorCannotDeactivateAPerson()
    {
        var registry = await database.RegistryAsync();
        var actor = await DeactivatedActorAsync(registry);
        var id = await NewPersonAsync(registry);

        var result = await registry.DeactivateAsync(id, CustodyAuthor.Person(actor), TestContext.Current.CancellationToken);

        Assert.Equal(PersonOutcome.ActorDeactivated, result.Outcome);
        Assert.True((await database.PersonAsync(id))!.Active);
        _ = Assert.Single(await database.ChangesOfAsync(id));
    }

    [DatabaseFact]
    public async Task DeactivatedActorCannotChangeAFunction()
    {
        var registry = await database.RegistryAsync();
        var actor = await DeactivatedActorAsync(registry);
        var id = await NewPersonAsync(registry);

        var result = await registry.ChangeFunctionAsync(id, Arbitration, CustodyAuthor.Person(actor), TestContext.Current.CancellationToken);

        Assert.Equal(PersonOutcome.ActorDeactivated, result.Outcome);
        Assert.Equal(Annotation, (await database.PersonAsync(id))!.Function);
        _ = Assert.Single(await database.ChangesOfAsync(id));
    }

    [DatabaseFact]
    public async Task UnregisteredActorIsStillRefusedAsUnknown()
    {
        var registry = await database.RegistryAsync();
        var id = await NewPersonAsync(registry);

        var result = await registry.ChangeFunctionAsync(id, Arbitration, CustodyAuthor.Person(long.MaxValue), TestContext.Current.CancellationToken);

        Assert.Equal(PersonOutcome.ActorUnknown, result.Outcome);
    }

    [DatabaseFact]
    public async Task ActiveActorStillActsAndTheChangeNamesThem()
    {
        var registry = await database.RegistryAsync();
        var actor = await NewPersonAsync(registry);
        var id = await NewPersonAsync(registry);

        var result = await registry.ChangeFunctionAsync(id, Arbitration, CustodyAuthor.Person(actor), TestContext.Current.CancellationToken);

        Assert.Equal(PersonOutcome.Done, result.Outcome);
        Assert.Equal(actor, (await database.ChangesOfAsync(id))[^1].ActorId);
    }

    [DatabaseFact]
    public async Task PersonMayDeactivateThemselves()
    {
        var registry = await database.RegistryAsync();
        var id = await NewPersonAsync(registry);

        var result = await registry.DeactivateAsync(id, CustodyAuthor.Person(id), TestContext.Current.CancellationToken);

        Assert.Equal(PersonOutcome.Done, result.Outcome);
    }

    private static async Task<long> DeactivatedActorAsync(PersonRegistry registry)
    {
        var actor = await NewPersonAsync(registry);
        _ = PeopleDatabase.Id(await registry.DeactivateAsync(actor, CustodyAuthor.System, TestContext.Current.CancellationToken));
        return actor;
    }

    private static async Task<long> NewPersonAsync(PersonRegistry registry) =>
        PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), Annotation), CustodyAuthor.System, TestContext.Current.CancellationToken));
}
