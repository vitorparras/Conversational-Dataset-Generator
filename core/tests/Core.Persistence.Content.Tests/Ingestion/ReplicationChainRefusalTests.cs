using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Content;
using Core.Persistence.Custody;
using Core.Persistence.Tests.Content;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>When the database refuses the recording of the replicated content because the chain is broken, the store says so with the refusal of the chain and nothing else.</summary>
public sealed class ReplicationChainRefusalTests
{
    private const string RefuseWhileBroken =
        "CREATE FUNCTION public.refuse_replication_test() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'the chain is broken' USING ERRCODE = 'CE002'; END $$; "
        + "CREATE TRIGGER replication_test_refusal BEFORE INSERT ON content.replication_occurrence FOR EACH ROW EXECUTE FUNCTION public.refuse_replication_test()";

    [DatabaseFact]
    public async Task ARefusalOfTheChainFromTheDatabaseIsTheRefusalOfTheChainOfTheStore()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var store = new ReplicationStore(world.DataSource);
        var batch = await IngestionWorld.OpenAsync(world);
        var item = await IngestionWorld.AddItemAsync(world, batch);
        await RunAsSuperuserAsync(world, RefuseWhileBroken);
        StoredOccurrence[] occurrences = [new(item, 0, new string('a', 64), new string('b', 64), 40, new DateTimeOffset(2031, 5, 6, 9, 0, 0, TimeSpan.Zero))];

        _ = await Assert.ThrowsAsync<CustodyChainBrokenException>(
            () => store.RecordAsync(new(1, batch), occurrences, [], TestContext.Current.CancellationToken));
    }

    [SuppressMessage("Security", "CA2100", Justification = "The statements are literals of the tests, never input.")]
    private static async Task RunAsSuperuserAsync(ContentWorld world, string statement)
    {
        await using var connection = await Sql.OpenAsync(world.Database.SuperuserConnectionString);
        await using var command = new NpgsqlCommand(statement, connection);
        _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
