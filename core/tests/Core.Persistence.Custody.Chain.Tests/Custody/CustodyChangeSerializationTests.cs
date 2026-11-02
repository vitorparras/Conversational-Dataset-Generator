using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Custody;

/// <summary>A change refused by the server as a serialization failure is told as the chain being busy, and neither it nor its event is kept (#58, CA-2).</summary>
[Trait("Category", "Database")]
public sealed class CustodyChangeSerializationTests : IClassFixture<CustodyDatabase>
{
    private const string RefusesToSerialize = "DO $$ BEGIN RAISE EXCEPTION 'could not serialize' USING ERRCODE = '40001'; END $$";

    private readonly CustodyDatabase database;

    public CustodyChangeSerializationTests(CustodyDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task ChangeRefusedBeforeItsEventIsToldTheChainIsBusyAndWritesNoEvent()
    {
        var before = (await database.EventsAsync()).Count;

        var result = await CustodyTransaction.AppendAfterAsync(
            await database.DataSourceAsync(),
            async scope =>
            {
                await RefuseAsync(scope);
                return Pending();
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(CustodyOutcome.Conflict, result?.Outcome);
        Assert.Equal(before, (await database.EventsAsync()).Count);
    }

    [DatabaseFact]
    public async Task ChangeRefusedAfterItsEventIsToldTheChainIsBusyAndTheEventIsNotKept()
    {
        var before = (await database.EventsAsync()).Count;

        var result = await CustodyTransaction.AppendBeforeAsync(
            await database.DataSourceAsync(),
            Pending(),
            async scope =>
            {
                await RefuseAsync(scope);
                return true;
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(CustodyOutcome.Conflict, result?.Outcome);
        Assert.Equal(before, (await database.EventsAsync()).Count);
    }

    private static async Task RefuseAsync(CustodyScope scope)
    {
        var command = scope.CreateCommand(RefusesToSerialize);
        await using (command)
        {
            _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
    }

    private static CustodyEvent Pending() =>
        CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.Person, 13001), CustodyStep.PersonCreated, CustodyReason.AdministrativeAct);
}
