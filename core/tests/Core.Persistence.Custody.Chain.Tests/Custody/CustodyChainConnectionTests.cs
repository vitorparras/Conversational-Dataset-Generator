using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Custody;

/// <summary>
/// The case that failed the property of the chain on a loaded CI run (#529): the writers of a case met at the chain and each append opened a connection of its own,
/// and under the load the opening of one outlasted the 15 seconds Npgsql allows. The seed of that run could not replay it, since what failed was the time the server
/// took to take a connection and not the plan; so the case is fixed here as the plan that asks for the most appends and the number of connections the writers open,
/// which does not depend on the load.
/// </summary>
[Trait("Category", "Database")]
public sealed class CustodyChainConnectionTests : IClassFixture<CustodyDatabase>
{
    private const int Writers = 5;

    private readonly CustodyDatabase database;

    public CustodyChainConnectionTests(CustodyDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task WritersThatMeetAtTheChainOpenNoMoreConnectionsThanTheyMayHaveInFlight()
    {
        var people = new PlannedPeople(await database.PersonAsync(active: true), await database.PersonAsync(active: false));
        var writers = await ConcurrentWriters.PooledAsync(database, people);
        var plan = new WriterPlan([.. Enumerable.Repeat(Appends(), Writers)]);

        var run = await Task.Run(() => writers.Run(plan), TestContext.Current.CancellationToken);

        Assert.Equal(Writers * WriterPlan.MostAppendsPerWriter, run.Answers.Count);
        Assert.InRange(database.PhysicalConnections, 1, ConcurrentWriters.MostInFlight);
    }

    private static IReadOnlyList<PlannedAppend> Appends() =>
        [.. Enumerable.Repeat(new PlannedAppend(PlannedAuthor.System, Corrects: false), WriterPlan.MostAppendsPerWriter)];
}
