using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Core.Persistence.Tests.People;

namespace Core.Persistence.Tests.Notices;

/// <summary>
/// The verdict that the chain failed (issue 595, CA-3, policy 005) tells every active person that the writing is suspended, in the transaction of the verdict, and does not tell the same person twice
/// while the notice is unread. A failed verdict breaks the whole chain of the database, so this class runs the story once.
/// </summary>
[Trait("Category", "Database")]
public sealed class PersonNoticesChainTests : IClassFixture<PeopleDatabase>
{
    private readonly PeopleDatabase database;

    public PersonNoticesChainTests(PeopleDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task AFailedVerdictTellsEachActivePersonOnceAndNotTheDeactivatedOne()
    {
        var (told, alsoTold, deactivated) = (await database.NewPersonAsync(), await database.NewPersonAsync(), await database.NewPersonAsync());
        var registry = await database.RegistryAsync();
        _ = await registry.DeactivateAsync(deactivated, CustodyAuthor.System, TestContext.Current.CancellationToken);

        Assert.Equal(CustodyOutcome.Appended, (await database.AppendToChainAsync(CustodyEvent.Verification(1, 1))).Outcome);
        Assert.Equal(CustodyOutcome.Appended, (await database.AppendToChainAsync(CustodyEvent.Verification(2, 1))).Outcome);

        var notices = await database.NoticesAsync();
        var one = Assert.Single(await notices.UnreadAsync(told, TestContext.Current.CancellationToken));
        Assert.Equal(NoticeCode.DomainReadOnly, one.Code);
        Assert.Null(one.SessionsEnded);
        _ = Assert.Single(await notices.UnreadAsync(alsoTold, TestContext.Current.CancellationToken));
        Assert.Empty(await notices.UnreadAsync(deactivated, TestContext.Current.CancellationToken));
    }
}
