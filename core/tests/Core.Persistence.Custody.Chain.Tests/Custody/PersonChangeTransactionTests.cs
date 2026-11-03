using System.Data;
using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Custody;

/// <summary>A change of a person and its event of custody are one transaction (#58, CA-7): the same one that wrote both, and either both exist or neither does.</summary>
[Trait("Category", "Database")]
public sealed class PersonChangeTransactionTests : IClassFixture<CustodyDatabase>
{
    private const string SameTransaction =
        "SELECT p.xmin::text = e.xmin::text FROM public.person p JOIN public.custody_event e ON e.item_id = p.id AND e.step = @step WHERE p.id = @id";

    private static readonly string Annotation = PersonFunctions.Accepted[0];
    private static readonly string Arbitration = PersonFunctions.Accepted[1];

    private readonly CustodyDatabase database;
    private long heldItem = 12_000_000;

    public PersonChangeTransactionTests(CustodyDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task CreationAndItsEventAreWrittenByTheSameTransaction()
    {
        var id = await CreateAsync();

        Assert.True(await database.FlagAsOwnerAsync(SameTransaction, ("step", "person_created"), ("id", id)));
    }

    [DatabaseFact]
    public async Task DeactivationAndItsEventAreWrittenByTheSameTransaction()
    {
        var id = await CreateAsync();
        _ = await (await database.RegistryAsync()).DeactivateAsync(id, CustodyAuthor.System, TestContext.Current.CancellationToken);

        Assert.True(await database.FlagAsOwnerAsync(SameTransaction, ("step", "person_deactivated"), ("id", id)));
    }

    [DatabaseFact]
    public async Task ChangeOfFunctionAndItsEventAreWrittenByTheSameTransaction()
    {
        var id = await CreateAsync();
        _ = await (await database.RegistryAsync()).ChangeFunctionAsync(id, Arbitration, CustodyAuthor.System, TestContext.Current.CancellationToken);

        Assert.True(await database.FlagAsOwnerAsync(SameTransaction, ("step", "person_function_changed"), ("id", id)));
    }

    [DatabaseFact]
    public async Task PasswordAndItsEventAreWrittenByTheSameTransaction()
    {
        var id = await CreateAsync();
        _ = await (await database.CredentialsAsync(2)).SetPasswordAsync(id, "ZQX-SENHA-5811", CustodyAuthor.System, TestContext.Current.CancellationToken);

        Assert.True(await database.FlagAsOwnerAsync(
            "SELECT c.xmin::text = e.xmin::text FROM public.person_credential c JOIN public.custody_event e ON e.item_id = c.person_id AND e.step = 'person_password_set' WHERE c.person_id = @id",
            ("id", id)));
    }

    [DatabaseFact]
    public async Task CreationThatMeetsAWriterAtTheLinkIsToldTheChainIsBusyAndLeavesNoPerson()
    {
        var registry = await database.RegistryAsync();
        var name = NewName();

        var result = await WhileTheLinkIsHeldAsync(() => registry.CreateAsync(new NewPerson(name, Annotation), CustodyAuthor.System, TestContext.Current.CancellationToken));

        Assert.Equal(PersonOutcome.ChainBusy, result.Outcome);
        Assert.Equal(0L, await database.CountAsOwnerAsync("SELECT count(*) FROM public.person WHERE name = @name", ("name", name)));
    }

    [DatabaseFact]
    public async Task DeactivationThatMeetsAWriterAtTheLinkLeavesThePersonActiveAndWithoutTheEvent()
    {
        var id = await CreateAsync();
        var registry = await database.RegistryAsync();

        var result = await WhileTheLinkIsHeldAsync(() => registry.DeactivateAsync(id, CustodyAuthor.System, TestContext.Current.CancellationToken));

        Assert.Equal(PersonOutcome.ChainBusy, result.Outcome);
        Assert.True(await database.FlagAsOwnerAsync("SELECT active FROM public.person WHERE id = @id", ("id", id)));
        Assert.Equal(1L, await database.CountAsOwnerAsync("SELECT count(*) FROM public.custody_event WHERE item_kind = 'person' AND item_id = @id", ("id", id)));
    }

    [DatabaseFact]
    public async Task ChangeOfFunctionThatMeetsAWriterAtTheLinkLeavesTheFunctionAsItWas()
    {
        var id = await CreateAsync();
        var registry = await database.RegistryAsync();

        var result = await WhileTheLinkIsHeldAsync(() => registry.ChangeFunctionAsync(id, Arbitration, CustodyAuthor.System, TestContext.Current.CancellationToken));

        Assert.Equal(PersonOutcome.ChainBusy, result.Outcome);
        Assert.Equal(Annotation, await database.TextAsOwnerAsync("SELECT function_name FROM public.person WHERE id = @id", ("id", id)));
    }

    [DatabaseFact]
    public async Task PasswordThatMeetsAWriterAtTheLinkLeavesNothingStored()
    {
        var id = await CreateAsync();
        var credentials = await database.CredentialsAsync(2);

        var result = await WhileTheLinkIsHeldAsync(() => credentials.SetPasswordAsync(id, "ZQX-SENHA-5812", CustodyAuthor.System, TestContext.Current.CancellationToken));

        Assert.Equal(PersonOutcome.ChainBusy, result.Outcome);
        Assert.Equal(0L, await database.CountAsOwnerAsync("SELECT count(*) FROM public.person_credential WHERE person_id = @id", ("id", id)));
    }

    [DatabaseFact]
    public async Task CreationsAtOnceLeaveAnEventForEachPersonThatExistsAndNoPersonWithout()
    {
        var registry = await database.RegistryAsync();
        var prefix = $"zqx-pessoa-{Guid.NewGuid():N}";
        var attempts = Enumerable.Range(0, 8)
            .Select(number => new NewPerson($"{prefix}-{number}", Annotation))
            .Select(person => Task.Run(() => registry.CreateAsync(person, CustodyAuthor.System, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken))
            .ToArray();

        var results = await Task.WhenAll(attempts);

        var created = results.Where(result => result.Outcome == PersonOutcome.Done).Select(result => result.PersonId!.Value).Order().ToList();
        var events = (await database.EventsAsync()).Where(row => row.Step == "person_created" && created.Contains(row.ItemId)).Select(row => row.ItemId).Order();
        Assert.All(results, result => Assert.Contains(result.Outcome, new[] { PersonOutcome.Done, PersonOutcome.ChainBusy }));
        Assert.Equal(created, events);
        Assert.Equal(created.Count, await database.CountAsOwnerAsync("SELECT count(*) FROM public.person WHERE name LIKE @prefix", ("prefix", prefix + "-%")));
        ChainIntegrity.Holds(await database.EventsAsync());
    }

    private static string NewName() => $"zqx-pessoa-{Guid.NewGuid():N}";

    private async Task<long> CreateAsync()
    {
        var result = await (await database.RegistryAsync()).CreateAsync(new NewPerson(NewName(), Annotation), CustodyAuthor.System, TestContext.Current.CancellationToken);
        Assert.Equal(PersonOutcome.Done, result.Outcome);
        return result.PersonId!.Value;
    }

    private async Task<PersonResult> WhileTheLinkIsHeldAsync(Func<Task<PersonResult>> change)
    {
        await using var held = await database.HoldAsync(Interlocked.Increment(ref heldItem), IsolationLevel.Serializable);
        var pending = change();
        Assert.True(await database.WaitForABlockedWriterAsync());
        await held.CommitAsync();
        return await pending;
    }
}
