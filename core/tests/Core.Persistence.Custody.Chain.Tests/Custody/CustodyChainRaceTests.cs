using System.Data;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Custody;

/// <summary>Two writers at the same link of the chain (#58, CA-2): either the chain stays valid with both in it, or one is told so; never both with the same previous event.</summary>
[Trait("Category", "Database")]
public sealed class CustodyChainRaceTests : IClassFixture<CustodyDatabase>
{
    private const int Rounds = 20;
    private const long FirstRaceItem = 8100;

    private readonly CustodyDatabase database;

    public CustodyChainRaceTests(CustodyDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task WriterThatReachesTheLinkAnotherHoldsIsToldTheChainIsBusyAndWritesNothing()
    {
        var chain = await database.ChainAsync();
        await using var held = await database.HoldAsync(7101, IsolationLevel.Serializable);
        var second = chain.AppendAsync(Event(7102), TestContext.Current.CancellationToken);
        Assert.True(await database.WaitForABlockedWriterAsync());

        await held.CommitAsync();
        var result = await second;

        Assert.Equal((CustodyOutcome.Conflict, null), (result.Outcome, result.Sequence));
        Assert.NotNull(result.Refusal);
        Assert.Equal([7101L], ItemsOf(await database.EventsAsync(), 7101, 7102));
    }

    [DatabaseFact]
    public async Task SameEventAskedAgainAfterTheBusyAnswerIsAppendedAndTheChainHoldsBoth()
    {
        var chain = await database.ChainAsync();
        await using var held = await database.HoldAsync(7201, IsolationLevel.Serializable);
        var refused = chain.AppendAsync(Event(7202), TestContext.Current.CancellationToken);
        Assert.True(await database.WaitForABlockedWriterAsync());
        await held.CommitAsync();
        Assert.Equal(CustodyOutcome.Conflict, (await refused).Outcome);

        var again = await chain.AppendAsync(Event(7202), TestContext.Current.CancellationToken);

        Assert.Equal(CustodyOutcome.Appended, again.Outcome);
        Assert.Equal([7201L, 7202L], ItemsOf(await database.EventsAsync(), 7201, 7202));
        ChainIntegrity.Holds(await database.EventsAsync());
    }

    [DatabaseFact]
    public async Task WriterWaitingForOneThatGivesUpIsAppendedInItsPlace()
    {
        var chain = await database.ChainAsync();
        await using var held = await database.HoldAsync(7301, IsolationLevel.Serializable);
        var second = chain.AppendAsync(Event(7302), TestContext.Current.CancellationToken);
        Assert.True(await database.WaitForABlockedWriterAsync());

        await held.RollbackAsync();
        var result = await second;

        Assert.Equal(CustodyOutcome.Appended, result.Outcome);
        Assert.Equal([7302L], ItemsOf(await database.EventsAsync(), 7301, 7302));
        ChainIntegrity.Holds(await database.EventsAsync());
    }

    [DatabaseFact]
    public async Task SimultaneousWritersAreEitherBothInAValidChainOrTheLoserIsToldSoOverManyRounds()
    {
        var chain = await database.ChainAsync();
        var answers = new List<CustodyResult>();
        foreach (var round in Enumerable.Range(0, Rounds))
        {
            answers.AddRange(await RaceAsync(chain, FirstRaceItem + (2 * round)));
        }

        var rows = await database.EventsAsync();
        var raced = rows.Where(row => row.ItemId >= FirstRaceItem).ToList();
        var appended = answers.Where(answer => answer.Outcome == CustodyOutcome.Appended).ToList();
        Assert.All(answers, answer => Assert.Contains(answer.Outcome, new[] { CustodyOutcome.Appended, CustodyOutcome.Conflict }));
        Assert.Equal(appended.Select(answer => answer.Sequence).Order(), raced.Select(row => (long?)row.Sequence).Order());
        ChainIntegrity.Holds(rows);
    }

    private static async Task<CustodyResult[]> RaceAsync(CustodyChain chain, long firstItem)
    {
        var start = new TaskCompletionSource();
        var writers = new[] { firstItem, firstItem + 1 }
            .Select(item => Task.Run(
                async () =>
                {
                    await start.Task;
                    return await chain.AppendAsync(Event(item), TestContext.Current.CancellationToken);
                },
                TestContext.Current.CancellationToken))
            .ToArray();
        start.SetResult();
        return await Task.WhenAll(writers);
    }

    private static IEnumerable<long> ItemsOf(IEnumerable<EventRow> rows, long first, long last) =>
        rows.Select(row => row.ItemId).Where(item => item >= first && item <= last);

    private static CustodyEvent Event(long item) =>
        CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.Person, item), CustodyStep.PersonCreated, CustodyReason.AdministrativeAct);
}
