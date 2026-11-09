using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.People;

/// <summary>
/// The list of people (#599, CA-2): by name, each person with the function declared and whether they are active and nothing else, narrowed by a stretch of the name, by the function and by the state
/// together, a page of twenty at most, the page asked beyond the last being the last. The registry is shared, so each test names its people with a stem nobody else uses and filters by it.
/// </summary>
[Trait("Category", "Database")]
public sealed class PersonListingTests : IClassFixture<PeopleDatabase>
{
    private readonly PeopleDatabase database;

    public PersonListingTests(PeopleDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task ThePeopleOfAStemAreListedByNameWithTheFunctionAndTheStateAndNothingElse()
    {
        var stem = Stem();
        _ = await MakeAsync($"{stem}-b", "Auditoria");
        var second = await MakeAsync($"{stem}-a", "Anotação");
        _ = await (await database.RegistryAsync()).DeactivateAsync(second, CustodyAuthor.System, TestContext.Current.CancellationToken);

        var page = await ListAsync(new PersonFilter(stem, null, null), 1);

        Assert.Equal(
            [($"{stem}-a", "Anotação", false), ($"{stem}-b", "Auditoria", true)],
            page.Items.Select(person => (person.Name, person.Function, person.Active)));
        Assert.Equal((2, 1, 1), (page.Total, page.Page, page.Pages));
    }

    [DatabaseFact]
    public async Task TheStretchOfTheNameIsFoundAnywhereInItWithNoRegardToCaseAndPercentAndUnderscoreAreNotWildcards()
    {
        var stem = Stem();
        _ = await MakeAsync($"{stem}-Maria_Silva", "Anotação");
        _ = await MakeAsync($"{stem}-MariaXSilva", "Anotação");

        var inside = await ListAsync(new PersonFilter($"{stem.ToUpperInvariant()}-maria_", null, null), 1);
        var wildcard = await ListAsync(new PersonFilter($"{stem}-%", null, null), 1);

        Assert.Equal([$"{stem}-Maria_Silva"], inside.Names());
        Assert.Empty(wildcard.Items);
    }

    [DatabaseFact]
    public async Task TheFunctionAndTheStateNarrowTheListTogetherWithTheName()
    {
        var stem = Stem();
        _ = await MakeAsync($"{stem}-1", "Auditoria");
        _ = await MakeAsync($"{stem}-2", "Anotação");
        var off = await MakeAsync($"{stem}-3", "Auditoria");
        _ = await (await database.RegistryAsync()).DeactivateAsync(off, CustodyAuthor.System, TestContext.Current.CancellationToken);

        var audit = await ListAsync(new PersonFilter(stem, "Auditoria", null), 1);
        var activeAudit = await ListAsync(new PersonFilter(stem, "Auditoria", true), 1);
        var inactive = await ListAsync(new PersonFilter(stem, null, false), 1);

        Assert.Equal([$"{stem}-1", $"{stem}-3"], audit.Names());
        Assert.Equal([$"{stem}-1"], activeAudit.Names());
        Assert.Equal([$"{stem}-3"], inactive.Names());
    }

    [DatabaseFact]
    public async Task APageHoldsTwentyAndTheTotalCountsEveryPageAndAPageBeyondTheLastIsTheLastAndOneBelowTheFirstIsTheFirst()
    {
        var stem = Stem();
        for (var number = 10; number < 10 + PersonPage.Size + 5; number++)
        {
            _ = await MakeAsync($"{stem}-{number}", "Anotação");
        }

        var first = await ListAsync(new PersonFilter(stem, null, null), 1);
        var second = await ListAsync(new PersonFilter(stem, null, null), 2);
        var beyond = await ListAsync(new PersonFilter(stem, null, null), 99);
        var below = await ListAsync(new PersonFilter(stem, null, null), -4);

        Assert.Equal((20, 5, 25, 2), (first.Items.Count, second.Items.Count, first.Total, first.Pages));
        Assert.Equal((2, 5, 1, 20), (beyond.Page, beyond.Items.Count, below.Page, below.Items.Count));
        Assert.Empty(first.Names().Intersect(second.Names()));
    }

    [DatabaseFact]
    public async Task TheTotalAndTheItemsOfAPageAgreeWhilePeopleAreBeingRegistered()
    {
        var stem = Stem();
        var registering = Task.Run(async () =>
        {
            for (var number = 10; number < 10 + PersonPage.Size + 10; number++)
            {
                _ = await MakeAsync($"{stem}-{number}", "Anotação");
            }
        });
        var pages = new List<PersonPage>();
        while (!registering.IsCompleted)
        {
            pages.Add(await ListAsync(new PersonFilter(stem, null, null), 2));
        }

        await registering;

        Assert.All(pages, page => Assert.Equal(Math.Clamp(page.Total - ((page.Page - 1) * PersonPage.Size), 0, PersonPage.Size), page.Items.Count));
    }

    [DatabaseFact]
    public async Task TheNameOfAnIdentifierIsTheNameOfUseActiveOrNotAndNoneWhenNobodyHasIt()
    {
        var stem = Stem();
        var id = await MakeAsync($"{stem}-Maria José", "Auditoria");
        var registry = await database.RegistryAsync();
        _ = await registry.DeactivateAsync(id, CustodyAuthor.System, TestContext.Current.CancellationToken);

        var name = await registry.NameOfAsync(id, TestContext.Current.CancellationToken);
        var nobody = await registry.NameOfAsync(long.MaxValue, TestContext.Current.CancellationToken);

        Assert.Equal($"{stem}-Maria José", name);
        Assert.Null(nobody);
    }

    [DatabaseFact]
    public async Task ANameThatNobodyHasLeavesAnEmptyFirstPage()
    {
        var page = await ListAsync(new PersonFilter(Stem(), null, null), 3);

        Assert.Equal((0, 0, 1, 1), (page.Items.Count, page.Total, page.Page, page.Pages));
    }

    private static string Stem() => $"zqx-lista-{Guid.NewGuid():N}";

    private async Task<long> MakeAsync(string name, string function)
    {
        var made = await (await database.RegistryAsync()).CreateAsync(new NewPerson(name, function), CustodyAuthor.System, TestContext.Current.CancellationToken);
        return made.PersonId!.Value;
    }

    private async Task<PersonPage> ListAsync(PersonFilter filter, int page) =>
        await (await database.RegistryAsync()).ListAsync(filter, page, TestContext.Current.CancellationToken);
}

internal static class PersonPageNames
{
    internal static string[] Names(this PersonPage page) => [.. page.Items.Select(person => person.Name)];
}
