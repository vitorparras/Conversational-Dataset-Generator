using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Custody;

/// <summary>Every change of a person leaves its event in the chain, in the transaction that writes the change (#58, CA-7), and a change that is refused leaves none.</summary>
[Trait("Category", "Database")]
public sealed class PersonChangesChainTests : IClassFixture<CustodyDatabase>
{
    private const string Password = "ZQX-SENHA-5810";
    private const int Factor = 2;

    private static readonly string Annotation = PersonFunctions.Accepted[0];
    private static readonly string Arbitration = PersonFunctions.Accepted[1];

    private readonly CustodyDatabase database;

    public PersonChangesChainTests(CustodyDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task CreatingAPersonLeavesTheEventOfTheCreationSignedBySystemWhenNobodyActs()
    {
        var registry = await database.RegistryAsync();

        var id = Id(await registry.CreateAsync(new NewPerson(NewName(), Annotation), actorId: null, TestContext.Current.CancellationToken));

        var row = Single(await database.EventsAsync(), id);
        Assert.Equal(("person", "person_created", "administrative_act", true, null), (row.ItemKind, row.Step, row.Reason, row.ActorSystem, row.ActorPersonId));
    }

    [DatabaseFact]
    public async Task CreatingAPersonLeavesTheEventSignedByTheActiveActor()
    {
        var registry = await database.RegistryAsync();
        var actor = Id(await registry.CreateAsync(new NewPerson(NewName(), Arbitration), actorId: null, TestContext.Current.CancellationToken));

        var id = Id(await registry.CreateAsync(new NewPerson(NewName(), Annotation), actor, TestContext.Current.CancellationToken));

        var row = Single(await database.EventsAsync(), id);
        Assert.Equal((actor, false), (row.ActorPersonId, row.ActorSystem));
    }

    [DatabaseFact]
    public async Task DeactivatingAPersonLeavesTheEventOfTheDeactivation()
    {
        var registry = await database.RegistryAsync();
        var id = Id(await registry.CreateAsync(new NewPerson(NewName(), Annotation), actorId: null, TestContext.Current.CancellationToken));

        _ = Id(await registry.DeactivateAsync(id, actorId: null, TestContext.Current.CancellationToken));

        var steps = (await database.EventsAsync()).Where(row => row.ItemId == id).Select(row => row.Step);
        Assert.Equal(["person_created", "person_deactivated"], steps);
    }

    [DatabaseFact]
    public async Task PersonWhoDeactivatesThemselvesSignsTheEventAsThemselves()
    {
        var registry = await database.RegistryAsync();
        var id = Id(await registry.CreateAsync(new NewPerson(NewName(), Annotation), actorId: null, TestContext.Current.CancellationToken));

        _ = Id(await registry.DeactivateAsync(id, id, TestContext.Current.CancellationToken));

        var row = (await database.EventsAsync()).Single(candidate => candidate.ItemId == id && candidate.Step == "person_deactivated");
        Assert.Equal((id, false), (row.ActorPersonId, row.ActorSystem));
    }

    [DatabaseFact]
    public async Task ChangingTheFunctionLeavesTheEventOfTheChange()
    {
        var registry = await database.RegistryAsync();
        var id = Id(await registry.CreateAsync(new NewPerson(NewName(), Annotation), actorId: null, TestContext.Current.CancellationToken));

        _ = Id(await registry.ChangeFunctionAsync(id, Arbitration, actorId: null, TestContext.Current.CancellationToken));

        var steps = (await database.EventsAsync()).Where(row => row.ItemId == id).Select(row => row.Step);
        Assert.Equal(["person_created", "person_function_changed"], steps);
    }

    [DatabaseFact]
    public async Task SettingAPasswordLeavesTheEventAndNothingOfThePassword()
    {
        var registry = await database.RegistryAsync();
        var credentials = await database.CredentialsAsync(Factor);
        var id = Id(await registry.CreateAsync(new NewPerson(NewName(), Annotation), actorId: null, TestContext.Current.CancellationToken));

        _ = Id(await credentials.SetPasswordAsync(id, Password, actorId: null, TestContext.Current.CancellationToken));

        var row = (await database.EventsAsync()).Single(candidate => candidate.ItemId == id && candidate.Step == "person_password_set");
        Assert.DoesNotContain(Password, string.Join('|', row.ItemKind, row.Step, row.Reason, row.RecordedBy), StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task RecomputingAPasswordAtANewFactorLeavesNoEvent()
    {
        var registry = await database.RegistryAsync();
        var id = Id(await registry.CreateAsync(new NewPerson(NewName(), Annotation), actorId: null, TestContext.Current.CancellationToken));
        _ = Id(await (await database.CredentialsAsync(Factor)).SetPasswordAsync(id, Password, actorId: null, TestContext.Current.CancellationToken));
        var before = (await database.EventsAsync()).Count;

        _ = await (await database.CredentialsAsync(Factor + 1)).VerifyAsync(id, Password, TestContext.Current.CancellationToken);

        Assert.Equal(before, (await database.EventsAsync()).Count);
    }

    [DatabaseFact]
    public async Task ChangeRefusedForADeactivatedActorLeavesNeitherThePersonNorTheEvent()
    {
        var registry = await database.RegistryAsync();
        var actor = await database.PersonAsync(active: false);
        var before = (await database.EventsAsync()).Count;

        var result = await registry.CreateAsync(new NewPerson(NewName(), Annotation), actor, TestContext.Current.CancellationToken);

        Assert.Equal(PersonOutcome.ActorDeactivated, result.Outcome);
        Assert.Equal(before, (await database.EventsAsync()).Count);
    }

    [DatabaseFact]
    public async Task ChangeOfAPersonWhoDoesNotExistLeavesNoEvent()
    {
        var registry = await database.RegistryAsync();
        var before = (await database.EventsAsync()).Count;

        var result = await registry.DeactivateAsync(long.MaxValue, actorId: null, TestContext.Current.CancellationToken);

        Assert.Equal(PersonOutcome.NotFound, result.Outcome);
        Assert.Equal(before, (await database.EventsAsync()).Count);
    }

    [DatabaseFact]
    public async Task NameTakenLeavesNeitherASecondPersonNorAnEvent()
    {
        var registry = await database.RegistryAsync();
        var name = NewName();
        _ = Id(await registry.CreateAsync(new NewPerson(name, Annotation), actorId: null, TestContext.Current.CancellationToken));
        var before = (await database.EventsAsync()).Count;

        var result = await registry.CreateAsync(new NewPerson(name, Annotation), actorId: null, TestContext.Current.CancellationToken);

        Assert.Equal(PersonOutcome.NameTaken, result.Outcome);
        Assert.Equal(before, (await database.EventsAsync()).Count);
    }

    private static string NewName() => $"zqx-pessoa-{Guid.NewGuid():N}";

    private static long Id(PersonResult result)
    {
        Assert.Equal(PersonOutcome.Done, result.Outcome);
        return result.PersonId!.Value;
    }

    private static EventRow Single(IEnumerable<EventRow> rows, long itemId) => rows.Single(row => row.ItemId == itemId);
}
