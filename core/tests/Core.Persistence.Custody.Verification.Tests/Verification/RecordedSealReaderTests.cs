using Core.Persistence.Custody;
using Core.Persistence.Custody.Verification;
using Core.Persistence.Tests.Custody;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Verification;

/// <summary>The reading of when the chain last recorded a seal (#62, part B1): what only the chain can say, for the manual situation to tell the age of the seal a verification stands on.</summary>
[Trait("Category", "Database")]
public sealed class RecordedSealReaderTests
{
    [DatabaseFact]
    public async Task ChainThatRecordedNoSealHasNoMoment()
    {
        await using var database = new CustodyDatabase();

        Assert.Null(await new RecordedSealReader(await database.DataSourceAsync()).LastAsync(TestContext.Current.CancellationToken));
    }

    [DatabaseFact]
    public async Task MomentIsThatOfTheLastRecordOfASealAndNotOfAnotherEvent()
    {
        await using var database = new CustodyDatabase();
        var sealer = new ChainSealer(await database.DataSourceAsync(), TimeProvider.System);
        _ = await sealer.RecordAsync(CustodyAuthor.System, TestContext.Current.CancellationToken);
        var second = await sealer.RecordAsync(CustodyAuthor.System, TestContext.Current.CancellationToken);
        _ = await new CustodyVerifier(await database.DataSourceAsync(), TimeProvider.System).VerifyAsync(TestContext.Current.CancellationToken);

        var last = await new RecordedSealReader(await database.DataSourceAsync()).LastAsync(TestContext.Current.CancellationToken);

        var events = await database.EventsAsync();
        Assert.Equal(events.Single(row => row.Sequence == second.Recorded.Sequence).OccurredAt, last);
    }

    [Fact]
    public void TheReaderNeedsItsDataSource() =>
        Assert.Equal("dataSource", Assert.Throws<ArgumentNullException>(() => new RecordedSealReader(null!)).ParamName);
}
