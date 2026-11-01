using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Custody;

/// <summary>What the chain keeps of an event is the item, the step and the reason, from lists the code declares: no text of the material and no name of a person (#58, CA-5, requisito 021).</summary>
[Trait("Category", "Database")]
public sealed class CustodyContentTests : IClassFixture<CustodyDatabase>
{
    private readonly CustodyDatabase database;

    public CustodyContentTests(CustodyDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task OnlyTheDeclaredColumnsOfTheChainCanHoldText()
    {
        var textColumns = await database.TextAsOwnerAsync(
            "SELECT string_agg(column_name::text, ',' ORDER BY column_name) FROM information_schema.columns " +
            "WHERE table_schema = 'public' AND table_name = 'custody_event' AND data_type IN ('text', 'character varying', 'character', 'json', 'jsonb', 'xml')");

        Assert.Equal("item_kind,reason,recorded_by,step", textColumns);
    }

    [DatabaseFact]
    public async Task EventsOfAPersonWhoActsHoldNoNameOfThemNorOfTheOneTheyActOn()
    {
        var registry = await database.RegistryAsync();
        var (actorName, targetName) = (NewName(), NewName());
        var actor = await registry.CreateAsync(new NewPerson(actorName, PersonFunctions.Accepted[1]), CustodyAuthor.System, TestContext.Current.CancellationToken);
        var target = await registry.CreateAsync(new NewPerson(targetName, PersonFunctions.Accepted[0]), CustodyAuthor.Person(actor.PersonId!.Value), TestContext.Current.CancellationToken);
        _ = await registry.DeactivateAsync(target.PersonId!.Value, CustodyAuthor.Person(actor.PersonId.Value), TestContext.Current.CancellationToken);

        var rows = (await database.EventsAsync()).Where(row => row.ItemId == target.PersonId || row.ActorPersonId == actor.PersonId);

        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.DoesNotContain("zqx-pessoa", Text(row), StringComparison.Ordinal));
    }

    [DatabaseFact]
    public async Task EventAboutAPersonNamesThePersonOnlyByTheNumberOfTheItem()
    {
        var registry = await database.RegistryAsync();
        var created = await registry.CreateAsync(new NewPerson(NewName(), PersonFunctions.Accepted[0]), CustodyAuthor.System, TestContext.Current.CancellationToken);

        var row = (await database.EventsAsync()).Single(candidate => candidate.ItemId == created.PersonId);

        Assert.Equal(("person", "person_created", "administrative_act", "app"), (row.ItemKind, row.Step, row.Reason, row.RecordedBy));
    }

    private static string Text(EventRow row) => string.Join('|', row.ItemKind, row.Step, row.Reason, row.RecordedBy);

    private static string NewName() => $"zqx-pessoa-{Guid.NewGuid():N}";
}
