using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Custody;

/// <summary>Who may sign (#58, plan 6): an active person, or the system; never a person deactivated, one nobody registered, or nobody, and the server holds this whatever the code does.</summary>
[Trait("Category", "Database")]
public sealed class CustodyAuthorRefusalTests : IClassFixture<CustodyDatabase>
{
    private readonly CustodyDatabase database;

    public CustodyAuthorRefusalTests(CustodyDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task PersonDeactivatedAfterSigningIsRefusedFromThenOnAndTheirEarlierEventStays()
    {
        var chain = await database.ChainAsync();
        var registry = await database.RegistryAsync();
        var author = (await registry.CreateAsync(new NewPerson($"zqx-pessoa-{Guid.NewGuid():N}", PersonFunctions.Accepted[0]), actorId: null, TestContext.Current.CancellationToken)).PersonId!.Value;
        var signed = await chain.AppendAsync(Event(CustodyAuthor.Person(author), 11101), TestContext.Current.CancellationToken);
        _ = await registry.DeactivateAsync(author, actorId: null, TestContext.Current.CancellationToken);

        var after = await chain.AppendAsync(Event(CustodyAuthor.Person(author), 11102), TestContext.Current.CancellationToken);

        var items = (await database.EventsAsync()).Where(row => row.ActorPersonId == author && row.ItemId is 11101 or 11102).Select(row => row.ItemId);
        Assert.Equal((CustodyOutcome.Appended, CustodyOutcome.ActorDeactivated), (signed.Outcome, after.Outcome));
        Assert.Equal([11101L], items);
    }

    [DatabaseFact]
    public async Task ServerRefusesAnInsertSignedByADeactivatedPersonWhateverTheCode()
    {
        var person = await database.PersonAsync(active: false);

        var refused = await database.RefusedToAppAsync(InsertBy(person, 11201));

        Assert.Equal((PostgresErrorCodes.CheckViolation, "custody_event_actor_active"), (refused.SqlState, refused.ConstraintName));
    }

    [DatabaseFact]
    public async Task ServerRefusesAnInsertSignedByAPersonNobodyRegisteredWhateverTheCode()
    {
        var refused = await database.RefusedToAppAsync(InsertBy(long.MaxValue, 11301));

        Assert.Equal((PostgresErrorCodes.ForeignKeyViolation, "custody_event_actor_fkey"), (refused.SqlState, refused.ConstraintName));
    }

    [DatabaseFact]
    public async Task ServerAcceptsAnInsertSignedByAnActivePerson()
    {
        var person = await database.PersonAsync(active: true);

        await database.RunAsOwnerAsync(InsertBy(person, 11401));

        Assert.Contains(await database.EventsAsync(), row => row.ItemId == 11401 && row.ActorPersonId == person);
    }

    [DatabaseFact]
    public async Task SystemSignsAsTheSystemAndAsNoPerson()
    {
        var chain = await database.ChainAsync();

        var result = await chain.AppendAsync(Event(CustodyAuthor.System, 11501), TestContext.Current.CancellationToken);

        var row = (await database.EventsAsync()).Single(candidate => candidate.Sequence == result.Sequence);
        Assert.Equal((true, null), (row.ActorSystem, row.ActorPersonId));
    }

    private static string InsertBy(long person, long item) =>
        $"INSERT INTO public.custody_event (actor_person_id, item_kind, item_id, step, reason) VALUES ({person}, 'person', {item}, 'person_created', 'administrative_act')";

    private static CustodyEvent Event(CustodyAuthor author, long item) =>
        CustodyEvent.Of(author, new CustodyItem(CustodyItemKind.Person, item), CustodyStep.PersonCreated, CustodyReason.AdministrativeAct);
}
