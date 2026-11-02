using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.People;

/// <summary>
/// The refusal of writing after a verification of the chain that failed (#61, CA-5) reaches the registry of people: a change of a person is answered with <see cref="PersonOutcome.ChainBroken" />
/// and the words of <c>PersonRefusals.ChainBroken</c>, and neither the person nor the record of the change is written. The failure breaks the whole chain of the database, so the class has one test.
/// </summary>
[Trait("Category", "Database")]
public sealed class PersonChainBrokenTests : IClassFixture<PeopleDatabase>
{
    private static readonly string Annotation = PersonFunctions.Accepted[0];
    private static readonly string Arbitration = PersonFunctions.Accepted[1];

    private readonly PeopleDatabase database;

    public PersonChainBrokenTests(PeopleDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task ChangeOfAPersonAfterARecordedFailureIsRefusedAsChainBrokenWithTheWordsOfTheRefusalAndWritesNothing()
    {
        var registry = await database.RegistryAsync();
        var toDeactivate = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), Annotation), CustodyAuthor.System, TestContext.Current.CancellationToken));
        var toChange = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), Annotation), CustodyAuthor.System, TestContext.Current.CancellationToken));
        var failure = await database.AppendToChainAsync(CustodyEvent.Verification(1, 1));
        Assert.Equal(CustodyOutcome.Appended, failure.Outcome);
        var (people, changes) = (await database.CountAsync("person"), await database.CountAsync("person_change"));

        var created = await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), Annotation), CustodyAuthor.System, TestContext.Current.CancellationToken);
        var deactivated = await registry.DeactivateAsync(toDeactivate, CustodyAuthor.System, TestContext.Current.CancellationToken);
        var changed = await registry.ChangeFunctionAsync(toChange, Arbitration, CustodyAuthor.System, TestContext.Current.CancellationToken);

        var refusal = PersonRefusals.ChainBroken();
        Assert.All(
            [created, deactivated, changed],
            result =>
            {
                Assert.Equal(PersonOutcome.ChainBroken, result.Outcome);
                Assert.Equal(refusal.Refusal, result.Refusal);
                Assert.Null(result.PersonId);
            });
        Assert.Equal((people, changes), (await database.CountAsync("person"), await database.CountAsync("person_change")));
        Assert.True((await database.PersonAsync(toDeactivate))!.Active);
        Assert.Equal(Annotation, (await database.PersonAsync(toChange))!.Function);
    }
}
