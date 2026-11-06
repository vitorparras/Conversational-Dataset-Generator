using Core.Persistence.Custody;
using Core.Persistence.Custody.Verification;
using Core.Persistence.Tests.Custody;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Verification;

/// <summary>
/// The history of the verifications the chain recorded (#605): newest first, each with who asked for it when someone did, the ones the host runs by itself with nobody, and the limit respected.
/// A verification asked for is recorded in the chain, by the chain, like any other. Each test has a database of its own.
/// </summary>
[Trait("Category", "Database")]
public sealed class RecordedVerificationAskerTests
{
    private const long Asker = 41;

    [DatabaseFact]
    public async Task AskedVerificationEntersTheHistoryWithWhoAskedAndTheOnesNobodyAskedForHaveNobody()
    {
        await using var database = new CustodyDatabase();
        var verifier = new CustodyVerifier(await database.DataSourceAsync(), TimeProvider.System);
        _ = await verifier.VerifyAsync(TestContext.Current.CancellationToken);
        var asked = await verifier.VerifyAsync(seal: null, Asker, TestContext.Current.CancellationToken);

        var history = await new RecordedVerificationReader(await database.DataSourceAsync()).HistoryAsync(10, TestContext.Current.CancellationToken);

        Assert.Equal(CustodyOutcome.Appended, asked.Recorded.Outcome);
        long?[] askers = [Asker, null];
        Assert.Equal(askers, [.. history.Select(entry => entry.RequestedBy)]);
        Assert.All(history, entry => Assert.True(entry.Passed));
    }

    [DatabaseFact]
    public async Task TheLastVerificationSaysWhoAskedAndTheNameIsAbsentWhenNobodyHasThatId()
    {
        await using var database = new CustodyDatabase();
        _ = await new CustodyVerifier(await database.DataSourceAsync(), TimeProvider.System).VerifyAsync(seal: null, Asker, TestContext.Current.CancellationToken);

        var last = await new RecordedVerificationReader(await database.DataSourceAsync()).LastAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(last);
        Assert.Equal((Asker, null), (last.RequestedBy, last.RequestedByName));
    }

    [DatabaseFact]
    public async Task HistoryIsNewestFirstAndStopsAtTheLimit()
    {
        await using var database = new CustodyDatabase();
        var verifier = new CustodyVerifier(await database.DataSourceAsync(), TimeProvider.System);
        for (var asker = 1; asker <= 3; asker++)
        {
            _ = await verifier.VerifyAsync(seal: null, asker, TestContext.Current.CancellationToken);
        }

        var history = await new RecordedVerificationReader(await database.DataSourceAsync()).HistoryAsync(2, TestContext.Current.CancellationToken);

        long?[] askers = [3, 2];
        Assert.Equal(askers, [.. history.Select(entry => entry.RequestedBy)]);
    }

    [DatabaseFact]
    public async Task ChainWithNoVerificationHasAnEmptyHistory()
    {
        await using var database = new CustodyDatabase();

        var history = await new RecordedVerificationReader(await database.DataSourceAsync()).HistoryAsync(5, TestContext.Current.CancellationToken);

        Assert.Empty(history);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void HistoryOfNoEntriesIsRefused(int limit)
    {
        using var source = NpgsqlDataSource.Create("Host=localhost");
        var reader = new RecordedVerificationReader(source);

        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = reader.HistoryAsync(limit, TestContext.Current.CancellationToken);
        });
    }
}
