using Core.Persistence.Tests.Fixtures;
using Core.Tests.Support.Harness;

namespace Core.Persistence.Tests.Custody;

/// <summary>The chain under sustained concurrent writing (#58, CA-3, decisão 0007); the property runs against a real server, so its cases are fewer than the declared ones.</summary>
[Trait("Category", "Database")]
public sealed class CustodyChainPropertyTests : IClassFixture<CustodyDatabase>
{
    private static readonly PropertyBudget Budget = new(Cases: 12, Deadline: TimeSpan.FromMinutes(3));

    private readonly CustodyDatabase database;

    public CustodyChainPropertyTests(CustodyDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    [Trait("Kind", "Property")]
    public async Task NoLinkEndsWithTwoSuccessorsWhateverTheWritersThatMeetAtTheChain()
    {
        var people = new PlannedPeople(await database.PersonAsync(active: true), await database.PersonAsync(active: false));
        var runner = new PropertyRunner(Environment.GetEnvironmentVariable, Budget, TestContext.Current.SendDiagnosticMessage);
        var invariants = new CustodyChainInvariants(runner, new ConcurrentWriters(await database.ChainAsync(), people), database.EventsAsync, people);

        invariants.NoLinkEndsWithTwoSuccessorsAndEveryAnswerIsTheChain();
    }
}
