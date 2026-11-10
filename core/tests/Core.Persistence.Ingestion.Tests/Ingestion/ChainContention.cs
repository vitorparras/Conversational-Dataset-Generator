using Core.Persistence.Custody;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// Another writer that reached the tip of the chain first and has not committed: what makes a write of the batches collide, with no clock to depend on.
/// The write under test starts while the other event is pending, waits on it, and meets its commit.
/// </summary>
internal static class ChainContention
{
    private const string Waiting = "SELECT count(*) FROM pg_stat_activity WHERE datname = @database AND wait_event_type = 'Lock'";

    /// <summary>Holds an event of the chain open, starts the write, waits until it is blocked on that event, commits the event and answers what the write answered.</summary>
    internal static async Task<T> WhilePendingAsync<T>(BatchesWorld world, Func<Task<T>> write)
    {
        var token = TestContext.Current.CancellationToken;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.Person, 9_000_001), CustodyStep.PersonCreated, CustodyReason.AdministrativeAct);
        var holder = CustodyTransaction.AppendBeforeAsync(
            world.DataSource,
            pending,
            async _ =>
            {
                started.SetResult();
                await release.Task;
                return true;
            },
            token);
        await started.Task;
        var writing = write();
        await WaitUntilSomeoneWaitsAsync(world);
        release.SetResult();
        _ = await holder;
        return await writing;
    }

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
            Assert.True(DateTimeOffset.UtcNow < deadline, "The write never waited on the pending event.");
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
    }
}
