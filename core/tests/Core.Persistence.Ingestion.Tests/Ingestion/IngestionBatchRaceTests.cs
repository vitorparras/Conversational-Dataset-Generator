using Core.Persistence.Content;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// A batch that closes while a writer is still adding to it (issue 74, criteria 2 and 5): the writer is not serializable and the closing is,
/// and the database must not let the closing work from a snapshot that does not hold what the writer added.
/// The closing that ran into such a writer is refused by the chain and leaves the batch as it was; the batches repeat it (issue 75), and the repeated one takes everything.
/// </summary>
public sealed class IngestionBatchRaceTests
{
    private const string AddItem =
        "INSERT INTO content.item_text (domain_id, batch_id, raw_text, dek_epoch, dek_wrapped) "
        + "VALUES (1, @batch, decode('a1', 'hex'), 1, decode('c3', 'hex'))";
    private const string Waiting =
        "SELECT count(*) FROM pg_stat_activity WHERE datname = @database AND wait_event_type = 'Lock' AND query LIKE 'UPDATE content.ingestion_batch%'";
    private const string InTheBatch = "SELECT count(*) FROM content.item_text WHERE batch_id = @batch";
    private const string Counted = "SELECT coalesce(sum(delta), 0)::text FROM public.content_domain WHERE domain_id = 1 AND table_name = 'item_text'";

    [DatabaseFact]
    public async Task RejectionThatMeetsAWriterNotYetCommittedIsRepeatedAndLeavesNoItemInTheRejectedBatch()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);

        var first = await CloseWhileAWriterCommitsAsync(world, batch, () => IngestionWorld.RejectAsync(world, batch, "write_failed"));

        Assert.Equal((BatchOutcome.Rejected, 0L), (first.Outcome, await ItemsInTheBatchAsync(world, batch)));
    }

    [DatabaseFact]
    public async Task AcceptanceThatMeetsAWriterNotYetCommittedIsRepeatedAndCountsEveryItem()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);

        var first = await CloseWhileAWriterCommitsAsync(world, batch, () => IngestionWorld.AcceptAsync(world, batch));

        Assert.Equal((BatchOutcome.Accepted, "1"), (first.Outcome, await world.ScalarAsync(Counted) as string));
    }

    /// <summary>
    /// A writer adds an item to the batch and does not commit; the closing starts and waits on it; the writer commits. The closing's result is what comes back.
    /// </summary>
    private static async Task<BatchResult> CloseWhileAWriterCommitsAsync(BatchesWorld world, long batch, Func<Task<BatchResult>> close)
    {
        var token = TestContext.Current.CancellationToken;
        var writer = await ContentTransaction.BeginAsync(world.DataSource, IngestionWorld.Domain, token);
        await using (writer)
        {
            await using (var insert = writer.CreateCommand(AddItem))
            {
                _ = insert.Parameters.AddWithValue("batch", batch);
                _ = await insert.ExecuteNonQueryAsync(token);
            }

            var closing = close();
            await WaitUntilSomeoneWaitsAsync(world);
            await writer.CommitAsync(token);
            return await closing;
        }
    }

    /// <summary>
    /// Waits, as the superuser (who is the one that sees what the sessions of other roles wait for), until the update that closes the batch waits on a lock.
    /// The only lock it can wait on is the turn of the batch that the writer holds.
    /// </summary>
    private static async Task WaitUntilSomeoneWaitsAsync(BatchesWorld world)
    {
        var server = await PostgresFixture.SharedAsync();
        await using var connection = new NpgsqlConnection(server.SuperuserConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(Waiting, connection);
        _ = command.Parameters.AddWithValue("database", new NpgsqlConnectionStringBuilder(world.Database.OwnerConnectionString).Database!);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while ((long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))! == 0)
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, "The closing never waited on the writer.");
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
    }

    private static async Task<long> ItemsInTheBatchAsync(BatchesWorld world, long batch)
    {
        await using var transaction = await ContentTransaction.BeginSystemAsync(world.DataSource, IngestionWorld.Domain, TestContext.Current.CancellationToken);
        await using var command = transaction.CreateCommand(InTheBatch);
        _ = command.Parameters.AddWithValue("batch", batch);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}
