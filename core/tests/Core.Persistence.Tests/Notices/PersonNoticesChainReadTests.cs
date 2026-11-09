using Core.Persistence.Custody;
using Core.Persistence.Tests.Fixtures;
using Core.Persistence.Tests.People;

namespace Core.Persistence.Tests.Notices;

/// <summary>After a person reads the notice that the writing is suspended, a new failed verdict tells them again, and the one who did not read it is not told a second time.</summary>
[Trait("Category", "Database")]
public sealed class PersonNoticesChainReadTests : IClassFixture<PeopleDatabase>
{
    private readonly PeopleDatabase database;

    public PersonNoticesChainReadTests(PeopleDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task ANewFailedVerdictTellsAgainWhoReadTheNoticeAndNotWhoDidNot()
    {
        var (reader, other) = (await database.NewPersonAsync(), await database.NewPersonAsync());
        _ = await database.AppendToChainAsync(CustodyEvent.Verification(1, 1));
        var notices = await database.NoticesAsync();
        _ = await notices.MarkReadAsync(reader, (await notices.UnreadAsync(reader, TestContext.Current.CancellationToken))[0].Id, TestContext.Current.CancellationToken);
        Assert.Empty(await notices.UnreadAsync(reader, TestContext.Current.CancellationToken));

        _ = await database.AppendToChainAsync(CustodyEvent.Verification(2, 1));

        _ = Assert.Single(await notices.UnreadAsync(reader, TestContext.Current.CancellationToken));
        _ = Assert.Single(await notices.UnreadAsync(other, TestContext.Current.CancellationToken));
    }
}
