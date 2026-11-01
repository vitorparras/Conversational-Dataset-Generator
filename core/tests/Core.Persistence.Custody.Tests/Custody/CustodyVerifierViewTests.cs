using System.Data;
using Core.Persistence.Custody;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Custody;

/// <summary>The verifier asks for what it needs (#61): a source of connections and a clock, and the view it reads the chain in is read-only, which the server itself tells it.</summary>
[Trait("Category", "Database")]
public sealed class CustodyVerifierViewTests
{
    // The policy lets the application read the events in a repeatable-read view only when that view is read-only, and in any other transaction as always; an insert is as always.
    private const string OnlyAReadOnlyViewReadsTheChain =
        "ALTER TABLE public.custody_event ENABLE ROW LEVEL SECURITY; "
        + "CREATE POLICY read_view_is_read_only ON public.custody_event FOR SELECT TO app "
        + "USING (pg_catalog.current_setting('transaction_isolation') <> 'repeatable read' OR pg_catalog.current_setting('transaction_read_only') = 'on'); "
        + "CREATE POLICY appends_as_always ON public.custody_event FOR INSERT TO app WITH CHECK (true)";

    [Fact]
    public void WithoutASourceOfConnectionsTheVerifierIsRefused() =>
        _ = Assert.Throws<ArgumentNullException>(() => new CustodyVerifier(null!, TimeProvider.System));

    [Fact]
    public async Task WithoutAClockTheVerifierIsRefused()
    {
        await using var database = new CustodyDatabase();

        _ = Assert.Throws<ArgumentNullException>(() => new CustodyVerifier(database.DataSourceAsync().GetAwaiter().GetResult(), null!));
    }

    [DatabaseFact]
    public async Task ChainIsReadInAViewTheServerCallsReadOnly()
    {
        await using var database = new CustodyDatabase();
        var chain = await database.ChainAsync();
        foreach (var item in new long[] { 1, 2, 3 })
        {
            var added = CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.Person, item), CustodyStep.PersonCreated, CustodyReason.AdministrativeAct);
            Assert.Equal(CustodyOutcome.Appended, (await chain.AppendAsync(added, TestContext.Current.CancellationToken)).Outcome);
        }

        await database.RunAsSuperuserAsync(OnlyAReadOnlyViewReadsTheChain);
        var seenWithoutReadOnly = await CountInARepeatableReadViewAsync(await database.DataSourceAsync());
        var verification = await new CustodyVerifier(await database.DataSourceAsync(), TimeProvider.System).VerifyAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0L, seenWithoutReadOnly);
        Assert.Equal((true, 3L), (verification.Passed, verification.EventsRead));
    }

    // The policy must hide the chain from a repeatable-read view that is not read-only, or the test would prove nothing about the verifier's.
    private static async Task<long> CountInARepeatableReadViewAsync(NpgsqlDataSource dataSource)
    {
        await using var connection = await dataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("SELECT count(*) FROM public.custody_event", connection, transaction);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}
