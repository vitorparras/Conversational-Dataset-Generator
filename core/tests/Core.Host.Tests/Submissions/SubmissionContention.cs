using System.Data;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Host.Tests.Submissions;

/// <summary>
/// Another writer that reached the tip of the chain first and has not committed: what makes a write of the batches collide, with no clock to depend on.
/// The write under test starts while the other event is pending, waits on it, and meets its commit.
/// </summary>
internal static class SubmissionContention
{
    private const string Pending =
        "INSERT INTO public.custody_event (actor_system, item_kind, item_id, step, reason) VALUES (true, 'person', 9000001, 'person_created', 'administrative_act')";
    private const string Waiting = "SELECT count(*) FROM pg_stat_activity WHERE datname = @database AND wait_event_type = 'Lock'";

    /// <summary>Holds an event of the chain open, starts the write, waits until it is blocked on that event, commits the event and answers what the write answered.</summary>
    /// <param name="world">The world whose chain the event is added to.</param>
    /// <param name="write">The write under test.</param>
    internal static async Task<T> WhilePendingAsync<T>(SubmissionWorld world, Func<Task<T>> write)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await world.DataSource.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, token);
        await using (var insert = new NpgsqlCommand(Pending, connection, transaction))
        {
            _ = await insert.ExecuteNonQueryAsync(token);
        }

        var writing = write();
        await WaitUntilSomeoneWaitsAsync(world);
        await transaction.CommitAsync(token);
        return await writing;
    }

    private static async Task WaitUntilSomeoneWaitsAsync(SubmissionWorld world)
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
