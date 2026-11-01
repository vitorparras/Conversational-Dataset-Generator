using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.People;

/// <summary>The registry against a real server, as the application's role (#109, CA-1 to CA-4): what it writes, what it refuses and that a refusal writes nothing.</summary>
[Trait("Category", "Database")]
public sealed class PersonRegistryDatabaseTests : IClassFixture<PeopleDatabase>
{
    private static readonly string Annotation = PersonFunctions.Accepted[0];
    private static readonly string Arbitration = PersonFunctions.Accepted[1];
    private static readonly string Audit = PersonFunctions.Accepted[4];

    private readonly PeopleDatabase database;

    public PersonRegistryDatabaseTests(PeopleDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task NewPersonReachesAnEmptyListAndNoException()
    {
        var registry = await database.RegistryAsync();
        var id = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), Annotation), CustodyAuthor.System, TestContext.Current.CancellationToken));

        var reach = PersonRegistry.ReachOf(id);

        Assert.NotNull(reach.DomainIds);
        Assert.Empty(reach.DomainIds);
    }

    [DatabaseFact]
    public async Task NewPersonIsStoredActiveWithTheNameAndTheFunctionGiven()
    {
        var registry = await database.RegistryAsync();
        var name = PeopleDatabase.NewName();
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);

        var id = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(name, Arbitration), CustodyAuthor.System, TestContext.Current.CancellationToken));

        var person = await database.PersonAsync(id);
        Assert.NotNull(person);
        Assert.Equal((name, Arbitration, true), (person.Name, person.Function, person.Active));
        Assert.Equal("app", person.CreatedBy);
        Assert.InRange(person.CreatedAt, before, DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [DatabaseFact]
    public async Task EveryFunctionOfThePolicyIsAcceptedAtCreation()
    {
        var registry = await database.RegistryAsync();

        foreach (var function in PersonFunctions.Accepted)
        {
            var id = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), function), CustodyAuthor.System, TestContext.Current.CancellationToken));

            Assert.Equal(function, (await database.PersonAsync(id))!.Function);
        }
    }

    [DatabaseFact]
    public async Task FunctionOutsideThePolicyAtCreationWritesNoPersonAndNoChange()
    {
        var registry = await database.RegistryAsync();
        var (people, changes) = (await database.CountAsync("person"), await database.CountAsync("person_change"));

        var result = await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), "Zqx-Funcao"), CustodyAuthor.System, TestContext.Current.CancellationToken);

        Assert.Equal(PersonOutcome.FunctionNotAccepted, result.Outcome);
        Assert.Equal((people, changes), (await database.CountAsync("person"), await database.CountAsync("person_change")));
    }

    [DatabaseFact]
    public async Task FunctionOutsideThePolicyAtChangeIsRefusedNamingTheOneAskedAndTheSixAcceptedAndKeepsTheFunction()
    {
        var registry = await database.RegistryAsync();
        var id = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), Annotation), CustodyAuthor.System, TestContext.Current.CancellationToken));
        var changes = await database.CountAsync("person_change");

        var result = await registry.ChangeFunctionAsync(id, "Zqx-Funcao", CustodyAuthor.System, TestContext.Current.CancellationToken);

        Assert.Equal(PersonOutcome.FunctionNotAccepted, result.Outcome);
        Assert.Contains("'Zqx-Funcao'", result.Refusal, StringComparison.Ordinal);
        Assert.All(PersonFunctions.Accepted, function => Assert.Contains(function, result.Refusal, StringComparison.Ordinal));
        Assert.Equal(Annotation, (await database.PersonAsync(id))!.Function);
        Assert.Equal(changes, await database.CountAsync("person_change"));
    }

    [DatabaseFact]
    public async Task DeactivatedPersonIsRefusedAtEntryAndStaysInTheRegistryAndInTheHistory()
    {
        var registry = await database.RegistryAsync();
        var id = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), Annotation), CustodyAuthor.System, TestContext.Current.CancellationToken));
        Assert.Equal(EntryStatus.Admitted, await registry.AdmitAsync(id, TestContext.Current.CancellationToken));

        _ = PeopleDatabase.Id(await registry.DeactivateAsync(id, CustodyAuthor.System, TestContext.Current.CancellationToken));

        Assert.Equal(EntryStatus.Deactivated, await registry.AdmitAsync(id, TestContext.Current.CancellationToken));
        Assert.False((await database.PersonAsync(id))!.Active);
        Assert.Equal(["created", "deactivated"], (await database.ChangesOfAsync(id)).Select(change => change.Kind));
    }

    [DatabaseFact]
    public async Task PersonNobodyRegisteredIsUnknownAtEntry()
    {
        var registry = await database.RegistryAsync();

        Assert.Equal(EntryStatus.Unknown, await registry.AdmitAsync(long.MaxValue, TestContext.Current.CancellationToken));
    }

    [DatabaseFact]
    public async Task CreationRecordsWhoOnWhomWhatAndWhen()
    {
        var registry = await database.RegistryAsync();
        var actor = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), Audit), CustodyAuthor.System, TestContext.Current.CancellationToken));
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);

        var id = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), Annotation), CustodyAuthor.Person(actor), TestContext.Current.CancellationToken));

        var change = Assert.Single(await database.ChangesOfAsync(id));
        Assert.Equal(("created", null, Annotation, actor, "app"), (change.Kind, change.PreviousFunction, change.NewFunction, change.ActorId, change.ChangedBy));
        Assert.InRange(change.ChangedAt, before, DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [DatabaseFact]
    public async Task CreationWithNobodyActingRecordsNoActor()
    {
        var registry = await database.RegistryAsync();

        var id = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), Annotation), CustodyAuthor.System, TestContext.Current.CancellationToken));

        Assert.Null(Assert.Single(await database.ChangesOfAsync(id)).ActorId);
    }

    [DatabaseFact]
    public async Task DeactivationRecordsWhoOnWhomWhatAndWhen()
    {
        var registry = await database.RegistryAsync();
        var actor = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), Audit), CustodyAuthor.System, TestContext.Current.CancellationToken));
        var id = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), Annotation), CustodyAuthor.System, TestContext.Current.CancellationToken));
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);

        var result = await registry.DeactivateAsync(id, CustodyAuthor.Person(actor), TestContext.Current.CancellationToken);

        Assert.Equal(id, PeopleDatabase.Id(result));
        var change = (await database.ChangesOfAsync(id))[^1];
        Assert.Equal(("deactivated", null, null, actor, "app"), (change.Kind, change.PreviousFunction, change.NewFunction, change.ActorId, change.ChangedBy));
        Assert.InRange(change.ChangedAt, before, DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [DatabaseFact]
    public async Task FunctionChangeRecordsWhoOnWhomFromWhatToWhatAndWhen()
    {
        var registry = await database.RegistryAsync();
        var actor = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), Audit), CustodyAuthor.System, TestContext.Current.CancellationToken));
        var id = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), Annotation), CustodyAuthor.System, TestContext.Current.CancellationToken));
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);

        var result = await registry.ChangeFunctionAsync(id, Arbitration, CustodyAuthor.Person(actor), TestContext.Current.CancellationToken);

        Assert.Equal(id, PeopleDatabase.Id(result));
        Assert.Equal(Arbitration, (await database.PersonAsync(id))!.Function);
        var change = (await database.ChangesOfAsync(id))[^1];
        Assert.Equal(("function_changed", Annotation, Arbitration, actor, "app"), (change.Kind, change.PreviousFunction, change.NewFunction, change.ActorId, change.ChangedBy));
        Assert.InRange(change.ChangedAt, before, DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [DatabaseFact]
    public async Task SuccessiveChangesKeepEachPreviousFunctionInOrder()
    {
        var registry = await database.RegistryAsync();
        var id = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), Annotation), CustodyAuthor.System, TestContext.Current.CancellationToken));

        _ = PeopleDatabase.Id(await registry.ChangeFunctionAsync(id, Arbitration, CustodyAuthor.System, TestContext.Current.CancellationToken));
        _ = PeopleDatabase.Id(await registry.ChangeFunctionAsync(id, Audit, CustodyAuthor.System, TestContext.Current.CancellationToken));

        var changes = await database.ChangesOfAsync(id);
        Assert.Equal(
            [("created", null, Annotation), ("function_changed", Annotation, Arbitration), ("function_changed", Arbitration, Audit)],
            changes.Select(change => (change.Kind, change.PreviousFunction, change.NewFunction)));
    }

    [DatabaseFact]
    public async Task NameAlreadyTakenIsRefusedAndWritesNothing()
    {
        var registry = await database.RegistryAsync();
        var name = PeopleDatabase.NewName();
        _ = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(name, Annotation), CustodyAuthor.System, TestContext.Current.CancellationToken));
        var (people, changes) = (await database.CountAsync("person"), await database.CountAsync("person_change"));

        var result = await registry.CreateAsync(new NewPerson(name, Arbitration), CustodyAuthor.System, TestContext.Current.CancellationToken);

        Assert.Equal(PersonOutcome.NameTaken, result.Outcome);
        Assert.Null(result.PersonId);
        Assert.NotNull(result.Refusal);
        Assert.Equal((people, changes), (await database.CountAsync("person"), await database.CountAsync("person_change")));
    }

    [DatabaseFact]
    public async Task TwoCreationsOfTheSameNameAtOnceLeaveOnePersonAndOneRecord()
    {
        var registry = await database.RegistryAsync();
        var name = PeopleDatabase.NewName();
        var (people, changes) = (await database.CountAsync("person"), await database.CountAsync("person_change"));

        var results = await Task.WhenAll(
            registry.CreateAsync(new NewPerson(name, Annotation), CustodyAuthor.System, TestContext.Current.CancellationToken),
            registry.CreateAsync(new NewPerson(name, Annotation), CustodyAuthor.System, TestContext.Current.CancellationToken));

        Assert.Equal([PersonOutcome.Done, PersonOutcome.NameTaken], results.Select(result => result.Outcome).Order());
        Assert.Equal((people + 1, changes + 1), (await database.CountAsync("person"), await database.CountAsync("person_change")));
    }

    [DatabaseFact]
    public async Task ActorNobodyRegisteredRefusesTheCreationAndWritesNothing()
    {
        var registry = await database.RegistryAsync();
        var (people, changes) = (await database.CountAsync("person"), await database.CountAsync("person_change"));

        var result = await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), Annotation), CustodyAuthor.Person(long.MaxValue), TestContext.Current.CancellationToken);

        Assert.Equal(PersonOutcome.ActorUnknown, result.Outcome);
        Assert.Equal((people, changes), (await database.CountAsync("person"), await database.CountAsync("person_change")));
    }

    [DatabaseFact]
    public async Task ActorNobodyRegisteredRefusesTheDeactivationAndKeepsThePersonActive()
    {
        var registry = await database.RegistryAsync();
        var id = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), Annotation), CustodyAuthor.System, TestContext.Current.CancellationToken));

        var result = await registry.DeactivateAsync(id, CustodyAuthor.Person(long.MaxValue), TestContext.Current.CancellationToken);

        Assert.Equal(PersonOutcome.ActorUnknown, result.Outcome);
        Assert.True((await database.PersonAsync(id))!.Active);
        _ = Assert.Single(await database.ChangesOfAsync(id));
    }

    [DatabaseFact]
    public async Task ActorNobodyRegisteredRefusesTheChangeAndKeepsTheFunction()
    {
        var registry = await database.RegistryAsync();
        var id = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), Annotation), CustodyAuthor.System, TestContext.Current.CancellationToken));

        var result = await registry.ChangeFunctionAsync(id, Arbitration, CustodyAuthor.Person(long.MaxValue), TestContext.Current.CancellationToken);

        Assert.Equal(PersonOutcome.ActorUnknown, result.Outcome);
        Assert.Equal(Annotation, (await database.PersonAsync(id))!.Function);
        _ = Assert.Single(await database.ChangesOfAsync(id));
    }

    [DatabaseFact]
    public async Task DeactivatingAPersonAlreadyDeactivatedIsRefusedAndAddsNoRecord()
    {
        var registry = await database.RegistryAsync();
        var id = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), Annotation), CustodyAuthor.System, TestContext.Current.CancellationToken));
        _ = PeopleDatabase.Id(await registry.DeactivateAsync(id, CustodyAuthor.System, TestContext.Current.CancellationToken));

        var result = await registry.DeactivateAsync(id, CustodyAuthor.System, TestContext.Current.CancellationToken);

        Assert.Equal(PersonOutcome.Deactivated, result.Outcome);
        Assert.Equal(2, (await database.ChangesOfAsync(id)).Count);
    }

    [DatabaseFact]
    public async Task TwoDeactivationsAtOnceLeaveOneRecord()
    {
        var registry = await database.RegistryAsync();
        var id = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), Annotation), CustodyAuthor.System, TestContext.Current.CancellationToken));

        var results = await Task.WhenAll(
            registry.DeactivateAsync(id, CustodyAuthor.System, TestContext.Current.CancellationToken),
            registry.DeactivateAsync(id, CustodyAuthor.System, TestContext.Current.CancellationToken));

        var outcomes = results.Select(result => result.Outcome).ToList();
        Assert.Single(outcomes, PersonOutcome.Done);
        Assert.Contains(outcomes.Single(outcome => outcome != PersonOutcome.Done), LosingOutcomes(PersonOutcome.Deactivated));
        Assert.Equal(["created", "deactivated"], (await database.ChangesOfAsync(id)).Select(change => change.Kind));
    }

    [DatabaseFact]
    public async Task DeactivatingAPersonNobodyRegisteredIsNotFound()
    {
        var registry = await database.RegistryAsync();
        var changes = await database.CountAsync("person_change");

        var result = await registry.DeactivateAsync(long.MaxValue, CustodyAuthor.System, TestContext.Current.CancellationToken);

        Assert.Equal(PersonOutcome.NotFound, result.Outcome);
        Assert.Equal(changes, await database.CountAsync("person_change"));
    }

    [DatabaseFact]
    public async Task ChangingTheFunctionOfAPersonNobodyRegisteredIsNotFound()
    {
        var registry = await database.RegistryAsync();
        var changes = await database.CountAsync("person_change");

        var result = await registry.ChangeFunctionAsync(long.MaxValue, Arbitration, CustodyAuthor.System, TestContext.Current.CancellationToken);

        Assert.Equal(PersonOutcome.NotFound, result.Outcome);
        Assert.Equal(changes, await database.CountAsync("person_change"));
    }

    [DatabaseFact]
    public async Task ChangingTheFunctionOfADeactivatedPersonIsRefusedAndKeepsTheFunction()
    {
        var registry = await database.RegistryAsync();
        var id = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), Annotation), CustodyAuthor.System, TestContext.Current.CancellationToken));
        _ = PeopleDatabase.Id(await registry.DeactivateAsync(id, CustodyAuthor.System, TestContext.Current.CancellationToken));

        var result = await registry.ChangeFunctionAsync(id, Arbitration, CustodyAuthor.System, TestContext.Current.CancellationToken);

        Assert.Equal(PersonOutcome.Deactivated, result.Outcome);
        Assert.Equal(Annotation, (await database.PersonAsync(id))!.Function);
        Assert.Equal(2, (await database.ChangesOfAsync(id)).Count);
    }

    [DatabaseFact]
    public async Task ChangingToTheFunctionThePersonHoldsIsRefusedAndAddsNoRecord()
    {
        var registry = await database.RegistryAsync();
        var id = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), Annotation), CustodyAuthor.System, TestContext.Current.CancellationToken));

        var result = await registry.ChangeFunctionAsync(id, Annotation, CustodyAuthor.System, TestContext.Current.CancellationToken);

        Assert.Equal(PersonOutcome.FunctionUnchanged, result.Outcome);
        _ = Assert.Single(await database.ChangesOfAsync(id));
    }

    [DatabaseFact]
    public async Task TwoChangesToTheSameFunctionAtOnceLeaveOneRecord()
    {
        var registry = await database.RegistryAsync();
        var id = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), Annotation), CustodyAuthor.System, TestContext.Current.CancellationToken));

        var results = await Task.WhenAll(
            registry.ChangeFunctionAsync(id, Audit, CustodyAuthor.System, TestContext.Current.CancellationToken),
            registry.ChangeFunctionAsync(id, Audit, CustodyAuthor.System, TestContext.Current.CancellationToken));

        var outcomes = results.Select(result => result.Outcome).ToList();
        Assert.Single(outcomes, PersonOutcome.Done);
        Assert.Contains(outcomes.Single(outcome => outcome != PersonOutcome.Done), LosingOutcomes(PersonOutcome.FunctionUnchanged));
        Assert.Equal(2, (await database.ChangesOfAsync(id)).Count);
    }

    /// <summary>The second of two writers is told what the first left, or that it met the first at the same link of the chain of custody and may repeat.</summary>
    private static PersonOutcome[] LosingOutcomes(PersonOutcome afterTheOther) => [afterTheOther, PersonOutcome.ChainBusy];
}
