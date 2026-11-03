using System.Data;
using Core.Persistence.Content;
using Core.Persistence.Content.Emptying;
using Core.Persistence.Custody;
using Core.Persistence.Tests.Fixtures;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// A map written while its item is being emptied (#73): the two are never both kept. The database stops one of them, and what is left is an item emptied with no map that holds
/// entries, whichever came first to commit.
/// </summary>
[Trait("Category", "Database")]
public sealed class ContentEmptyingRaceTests
{
    private const string Protocol = "PROT-2026/0042";
    private const string IntactMaps = "SELECT count(*) FROM content.reversal_map WHERE item_id = @id AND emptied_at IS NULL";
    private const string WaitingToInsert = "SELECT count(*) FROM pg_stat_activity WHERE datname = @database AND wait_event_type = 'Lock' AND query LIKE 'INSERT INTO content.reversal_map%'";
    private const string LockItem = "SELECT id FROM content.item_text WHERE id = @id FOR UPDATE";

    [DatabaseFact]
    public async Task MapNotYetCommittedWhenTheEmptyingCommitsIsStoppedAndNoMapStaysWhole()
    {
        await using var world = await ContentWorld.CreateAsync();
        var (curator, item) = await ItemAsync(world);
        var writing = await ContentTransaction.BeginAsync(world.DataSource, 1, IsolationLevel.Serializable, TestContext.Current.CancellationToken);
        await using (writing.ConfigureAwait(false))
        {
            await InsertMapAsync(writing, item);

            var emptied = await world.Emptier.EmptyAsync(new ItemEmptying(CustodyAuthor.Person(curator), 1, item, EmptyingMotive.ContractRequirement, Protocol), TestContext.Current.CancellationToken);
            var commit = await Record.ExceptionAsync(() => writing.CommitAsync(TestContext.Current.CancellationToken));

            var emptyingWon = emptied.Outcome == EmptyingOutcome.Emptied;
            var writeStopped = commit is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure };
            Assert.Equal((true, 0L), (emptyingWon == writeStopped, await world.OwnerInDomainAsync(1, IntactMaps, ("id", item))));
        }
    }

    [DatabaseFact]
    public async Task MapWrittenBehindAnEmptyingThatCommittedFirstIsWrittenAgainAndRefusedBecauseTheItemIsEmptied()
    {
        await using var world = await ContentWorld.CreateAsync();
        var (_, item) = await ItemAsync(world);
        var emptying = await ContentTransaction.BeginAsync(world.DataSource, 1, TestContext.Current.CancellationToken);
        await using (emptying.ConfigureAwait(false))
        {
            _ = await ScalarAsync(emptying, LockItem, item);
            var writing = world.Writer.AddReversalMapAsync(1, item, "mapa zqx", TestContext.Current.CancellationToken);
            await WaitForTheWriterToBlockAsync(world);

            _ = await ScalarAsync(emptying, ContentQueries.EmptyItemText, item);
            _ = await ScalarAsync(emptying, ContentQueries.EmptyReversalMaps, item);
            await emptying.CommitAsync(TestContext.Current.CancellationToken);

            _ = await Assert.ThrowsAsync<ItemEmptiedException>(() => writing);
            Assert.Equal(0L, await world.OwnerInDomainAsync(1, IntactMaps, ("id", item)));
        }
    }

    private static async Task<(long Curator, long Item)> ItemAsync(ContentWorld world)
    {
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText("texto bruto zqx", "texto sanitizado zqx", "texto pseudonimizado zqx"), TestContext.Current.CancellationToken);
        _ = await world.Writer.AddReversalMapAsync(1, item, "mapa de reversao zqx", TestContext.Current.CancellationToken);
        return (await world.PersonAsync("zqx-curator"), item);
    }

    private static async Task InsertMapAsync(ContentTransaction transaction, long item)
    {
        var command = transaction.CreateCommand(ContentQueries.InsertReversalMap);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("domain", NpgsqlDbType.Integer) { Value = 1 });
            _ = command.Parameters.Add(new NpgsqlParameter("item", NpgsqlDbType.Bigint) { Value = item });
            _ = command.Parameters.Add(new NpgsqlParameter("entries", NpgsqlDbType.Bytea) { Value = new byte[] { 1 } });
            _ = command.Parameters.Add(new NpgsqlParameter("epoch", NpgsqlDbType.Integer) { Value = 1 });
            _ = command.Parameters.Add(new NpgsqlParameter("wrapped", NpgsqlDbType.Bytea) { Value = new byte[] { 1 } });
            _ = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        }
    }

    private static async Task<object?> ScalarAsync(ContentTransaction transaction, string statement, long item)
    {
        var command = transaction.CreateCommand(statement);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = item });
            return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        }
    }

    private static async Task WaitForTheWriterToBlockAsync(ContentWorld world)
    {
        var server = await PostgresFixture.SharedAsync();
        await using var connection = new NpgsqlConnection(server.SuperuserConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var command = new NpgsqlCommand(WaitingToInsert, connection);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.AddWithValue("database", new NpgsqlConnectionStringBuilder(world.Database.OwnerConnectionString).Database!);
            var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
            while ((long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))! == 0)
            {
                Assert.True(DateTimeOffset.UtcNow < deadline, "The write of the map did not wait for the lock of the item.");
                await Task.Delay(50, TestContext.Current.CancellationToken);
            }
        }
    }
}
