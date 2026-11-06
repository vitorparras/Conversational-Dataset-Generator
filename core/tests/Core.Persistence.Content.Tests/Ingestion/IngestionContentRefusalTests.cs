using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Tests.Content;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>What the database refuses about the content of a batch whoever asks, the owner and a session that skips the ordinary triggers included (issue 74, criteria 2 and 5).</summary>
public sealed class IngestionContentRefusalTests
{
    private const string WithoutEvent = "CE005";
    private const string RejectedEvent =
        "INSERT INTO public.custody_event (actor_system, item_kind, item_id, step, reason, reference) VALUES (true, 'ingestion_batch', @event, 'batch_closed', 'batch_rejected', 'invalid_rows')";
    private const string Reject = "UPDATE content.ingestion_batch SET state = 'rejected', motive = 'invalid_rows' WHERE id = @id";
    private const string RemoveItem = "DELETE FROM content.item_text WHERE id = @id";
    private const string RemoveMap = "DELETE FROM content.reversal_map WHERE item_id = @id";

    [DatabaseFact]
    public async Task ItemAndMapOfABatchThatIsNotRejectedAreNeverRemovedNotEvenByTheOwnerOrAReplicaSession()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var open = await IngestionWorld.OpenAsync(world);
        var inOpen = await IngestionWorld.AddItemAsync(world, open);
        _ = await IngestionWorld.AddMapAsync(world, open, inOpen);
        var accepted = await IngestionWorld.OpenAsync(world);
        var inAccepted = await IngestionWorld.AddItemAsync(world, accepted);
        _ = await IngestionWorld.AddMapAsync(world, accepted, inAccepted);
        _ = await IngestionWorld.AcceptAsync(world, accepted);

        var refused = new[]
        {
            await Assert.ThrowsAsync<PostgresException>(() => AsReplicaAsync(world, RemoveMap, inOpen)),
            await Assert.ThrowsAsync<PostgresException>(() => AsReplicaAsync(world, RemoveItem, inOpen)),
            await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, RemoveMap, ("id", inAccepted))),
            await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, RemoveItem, ("id", inAccepted))),
            await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, RemoveItem, ("id", inAccepted))),
            await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, RemoveMap, ("id", inAccepted))),
            await Assert.ThrowsAsync<PostgresException>(() => AsReplicaAsync(world, RemoveItem, inAccepted)),
            await Assert.ThrowsAsync<PostgresException>(() => AsReplicaAsync(world, RemoveMap, inAccepted)),
        };

        Assert.All(refused, one => Assert.Equal(WithoutEvent, one.SqlState));
        Assert.Equal("1,1", await world.InDomainAsync(1, IngestionWorld.Reachable) as string);
    }

    [DatabaseFact]
    public async Task ItemOfARejectedBatchIsRemovedOnlyInTheTransactionOfTheEventOfTheRejection()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        var item = await IngestionWorld.AddItemAsync(world, batch);
        const string Closing = RejectedEvent + "; " + Reject;
        const string RemoveRejected = "DELETE FROM content.item_text WHERE id = @item RETURNING id";

        var withoutIt = await Assert.ThrowsAsync<PostgresException>(() => AsReplicaAsync(world, RemoveItem, item));
        var removed = await world.InDomainAfterAsync(Closing, 1, RemoveRejected, ("event", batch), ("id", batch), ("item", item));

        Assert.Equal((WithoutEvent, item), (withoutIt.SqlState, removed));
    }

    [SuppressMessage("Security", "CA2100", Justification = "The statements are literals of the tests, never input.")]
    private static async Task<object?> AsReplicaAsync(ContentWorld world, string statement, long id)
    {
        await using var connection = await Sql.OpenAsync(world.Database.SuperuserConnectionString);
        await using var replica = new NpgsqlCommand("SET session_replication_role = replica", connection);
        _ = await replica.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(statement, connection);
        if (statement.Contains("@id", StringComparison.Ordinal))
        {
            _ = command.Parameters.AddWithValue("id", id);
        }

        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }
}
