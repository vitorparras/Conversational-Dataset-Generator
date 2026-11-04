using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.People;

/// <summary>
/// The administration function in the registry (issue 65, part 2): the function of an active person is read, and the last active holder of the function is never deactivated,
/// whatever the order of two requests, while anybody else is and the plain deactivation of the registry is left as it was.
/// </summary>
[Trait("Category", "Database")]
public sealed class PersonRegistryAdministrationTests
{
    private static async Task<long> AdministratorAsync(PersonRegistry registry) =>
        PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), PersonFunctions.Administration), CustodyAuthor.System, TestContext.Current.CancellationToken));

    [DatabaseFact]
    public async Task TheFunctionOfAnActivePersonIsReadAndOfADeactivatedOneIsNot()
    {
        await using var database = new PeopleDatabase();
        var registry = await database.RegistryAsync();
        var id = await AdministratorAsync(registry);

        var active = await registry.ActiveFunctionOfAsync(id, TestContext.Current.CancellationToken);
        _ = await registry.DeactivateAsync(id, CustodyAuthor.System, TestContext.Current.CancellationToken);

        Assert.Equal((PersonFunctions.Administration, null), (active, await registry.ActiveFunctionOfAsync(id, TestContext.Current.CancellationToken)));
        Assert.Null(await registry.ActiveFunctionOfAsync(long.MaxValue, TestContext.Current.CancellationToken));
    }

    [DatabaseFact]
    public async Task TheLastActiveAdministratorIsRefusedWithItsOutcomeAndNothingIsWritten()
    {
        await using var database = new PeopleDatabase();
        var registry = await database.RegistryAsync();
        var id = await AdministratorAsync(registry);

        var refused = await registry.DeactivateKeepingAdministrationAsync(id, CustodyAuthor.System, TestContext.Current.CancellationToken);

        Assert.Equal((PersonOutcome.LastAdministrator, true, 1L), (refused.Outcome, (await database.PersonAsync(id))!.Active, await database.CountAsync("person_change")));
    }

    [DatabaseFact]
    public async Task AnAdministratorIsDeactivatedWhileAnotherIsActiveAndThenTheOtherIsKept()
    {
        await using var database = new PeopleDatabase();
        var registry = await database.RegistryAsync();
        var first = await AdministratorAsync(registry);
        var second = await AdministratorAsync(registry);

        var done = await registry.DeactivateKeepingAdministrationAsync(first, CustodyAuthor.Person(second), TestContext.Current.CancellationToken);
        var kept = await registry.DeactivateKeepingAdministrationAsync(second, CustodyAuthor.Person(second), TestContext.Current.CancellationToken);

        Assert.Equal((PersonOutcome.Done, PersonOutcome.LastAdministrator), (done.Outcome, kept.Outcome));
    }

    [DatabaseFact]
    public async Task APersonOfAnotherFunctionIsDeactivatedAndAnUnknownOneIsNotFound()
    {
        await using var database = new PeopleDatabase();
        var registry = await database.RegistryAsync();
        var id = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), PersonFunctions.Accepted[0]), CustodyAuthor.System, TestContext.Current.CancellationToken));

        var done = await registry.DeactivateKeepingAdministrationAsync(id, CustodyAuthor.System, TestContext.Current.CancellationToken);
        var again = await registry.DeactivateKeepingAdministrationAsync(id, CustodyAuthor.System, TestContext.Current.CancellationToken);
        var unknown = await registry.DeactivateKeepingAdministrationAsync(long.MaxValue, CustodyAuthor.System, TestContext.Current.CancellationToken);

        Assert.Equal((PersonOutcome.Done, PersonOutcome.Deactivated, PersonOutcome.NotFound), (done.Outcome, again.Outcome, unknown.Outcome));
    }

    [DatabaseFact]
    public async Task TwoAdministratorsDeactivatedAtOnceLeaveOneActive()
    {
        await using var database = new PeopleDatabase();
        var registry = await database.RegistryAsync();
        var first = await AdministratorAsync(registry);
        var second = await AdministratorAsync(registry);

        var results = await Task.WhenAll(
            registry.DeactivateKeepingAdministrationAsync(first, CustodyAuthor.System, TestContext.Current.CancellationToken),
            registry.DeactivateKeepingAdministrationAsync(second, CustodyAuthor.System, TestContext.Current.CancellationToken));

        var states = new[] { (await database.PersonAsync(first))!.Active, (await database.PersonAsync(second))!.Active };
        Assert.Equal(1, states.Count(active => active));
        Assert.Contains(results, result => result.Outcome == PersonOutcome.Done);
    }
}
