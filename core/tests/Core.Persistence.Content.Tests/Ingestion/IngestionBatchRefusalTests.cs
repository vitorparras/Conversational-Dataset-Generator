using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Content;
using Core.Persistence.Tests.Content;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>What the database refuses about the batches whoever asks, the owner and a session that skips the ordinary triggers included (issue 74, criteria 4 and 6).</summary>
public sealed class IngestionBatchRefusalTests
{
    private const string WithoutEvent = "CE005";
    private const string OutsideTheRules = "CE006";
    private const string AcceptedEvent =
        "INSERT INTO public.custody_event (actor_system, item_kind, item_id, step, reason) VALUES (true, 'ingestion_batch', @event, 'batch_closed', 'batch_accepted')";
    private const string RejectedEvent =
        "INSERT INTO public.custody_event (actor_system, item_kind, item_id, step, reason, reference) VALUES (true, 'ingestion_batch', @event, 'batch_closed', 'batch_rejected', 'invalid_rows')";
    private const string Close = "UPDATE content.ingestion_batch SET state = 'accepted' WHERE id = @id";
    private const string Reopen = "UPDATE content.ingestion_batch SET state = 'open', motive = NULL, closed_at = NULL WHERE id = @id";
    private const string Accept = "UPDATE content.ingestion_batch SET state = 'accepted', motive = NULL WHERE id = @id";
    private const string Reject = "UPDATE content.ingestion_batch SET state = 'rejected', motive = 'invalid_rows' WHERE id = @id";
    private const string RemoveBatch = "DELETE FROM content.ingestion_batch WHERE id = @id";
    private const string EmptyBatches = "TRUNCATE content.ingestion_batch";
    private const string RemoveItem = "DELETE FROM content.item_text WHERE id = @id";
    private const string RemoveMap = "DELETE FROM content.reversal_map WHERE item_id = @id";

    [DatabaseFact]
    public async Task OpeningWithoutItsEventInTheSameTransactionIsRefusedWhenTheTransactionEnds()
    {
        await using var world = await IngestionWorld.CreateAsync();

        var refused = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var transaction = await ContentTransaction.BeginAsync(world.DataSource, IngestionWorld.Domain, TestContext.Current.CancellationToken);
            await using var insert = transaction.CreateCommand("INSERT INTO content.ingestion_batch (domain_id, opened_by) VALUES (1, 1)");
            _ = await insert.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        });

        Assert.Equal((WithoutEvent, "0"), (refused.SqlState, await world.ScalarAsync("SELECT count(*)::text FROM content.ingestion_batch") as string));
    }

    [DatabaseFact]
    public async Task ApplicationCannotWriteTheStateOrTheMomentsWhenOpening()
    {
        await using var world = await IngestionWorld.CreateAsync();

        var state = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, "INSERT INTO content.ingestion_batch (domain_id, opened_by, state) VALUES (1, 1, 'accepted')"));
        var moment = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, "INSERT INTO content.ingestion_batch (domain_id, opened_by, opened_at) VALUES (1, 1, now())"));

        Assert.Equal((PostgresErrorCodes.InsufficientPrivilege, PostgresErrorCodes.InsufficientPrivilege), (state.SqlState, moment.SqlState));
    }

    [DatabaseFact]
    public async Task BatchBornClosedIsRefusedToTheOwnerToo()
    {
        await using var world = await IngestionWorld.CreateAsync();

        var refused = await Assert.ThrowsAsync<PostgresException>(
            () => world.OwnerInDomainAsync(1, "INSERT INTO content.ingestion_batch (domain_id, opened_by, state, closed_at) VALUES (1, 1, 'accepted', now())"));

        Assert.Equal(OutsideTheRules, refused.SqlState);
    }

    [DatabaseFact]
    public async Task ClosingWithoutTheEventOfTheClosingInTheSameTransactionIsRefused()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);

        var accepted = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, Close, ("id", batch)));
        var rejected = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, Reject, ("id", batch)));
        var byOwner = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, Close, ("id", batch)));

        Assert.Equal((WithoutEvent, WithoutEvent, WithoutEvent), (accepted.SqlState, rejected.SqlState, byOwner.SqlState));
    }

    [DatabaseFact]
    public async Task ClosingWithTheEventOfAnotherOutcomeOrAnotherBatchIsRefused()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        var other = await IngestionWorld.OpenAsync(world);

        var ofOther = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAfterAsync(AcceptedEvent, 1, Close, ("event", batch), ("id", other)));
        var ofAnotherOutcome = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAfterAsync(AcceptedEvent, 1, Reject, ("event", batch), ("id", batch)));

        Assert.Equal((WithoutEvent, WithoutEvent), (ofOther.SqlState, ofAnotherOutcome.SqlState));
    }

    [DatabaseFact]
    public async Task BatchThatClosedPassesToNothingElseNotEvenForTheOwnerOrAReplicaSession()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var accepted = await IngestionWorld.OpenAsync(world);
        _ = await IngestionWorld.AcceptAsync(world, accepted);
        var rejected = await IngestionWorld.OpenAsync(world);
        _ = await IngestionWorld.RejectAsync(world, rejected, "invalid_rows");
        var open = await IngestionWorld.OpenAsync(world);

        var byOwner = new[]
        {
            await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, Reopen, ("id", accepted))),
            await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, Reopen, ("id", rejected))),
            await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, Accept, ("id", rejected))),
            await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, Reject, ("id", accepted))),
            await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, Reopen, ("id", open))),
        };
        var asReplica = new[]
        {
            await Assert.ThrowsAsync<PostgresException>(() => AsReplicaAsync(world, Reopen, accepted)),
            await Assert.ThrowsAsync<PostgresException>(() => AsReplicaAsync(world, Accept, rejected)),
            await Assert.ThrowsAsync<PostgresException>(() => AsReplicaAsync(world, Reject, accepted)),
        };

        Assert.All(byOwner.Concat(asReplica), refused => Assert.Equal(OutsideTheRules, refused.SqlState));
    }

    [DatabaseFact]
    public async Task BatchIsNeverRemovedAndTheTableIsNeverEmptiedWhoeverAsks()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await IngestionWorld.OpenAsync(world);
        _ = await IngestionWorld.RejectAsync(world, batch, "invalid_rows");

        var byApplication = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, RemoveBatch, ("id", batch)));
        var byOwner = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, RemoveBatch, ("id", batch)));
        var emptiedByOwner = await Assert.ThrowsAsync<PostgresException>(() => world.ScalarAsync(EmptyBatches));
        var asReplica = await Assert.ThrowsAsync<PostgresException>(() => AsReplicaAsync(world, RemoveBatch, batch));
        var emptiedAsReplica = await Assert.ThrowsAsync<PostgresException>(() => AsReplicaAsync(world, EmptyBatches, batch));

        PostgresException[] all = [byApplication, byOwner, emptiedByOwner, asReplica, emptiedAsReplica];
        Assert.All(
            all,
            refused => Assert.True(refused.SqlState is OutsideTheRules or PostgresErrorCodes.InsufficientPrivilege or PostgresErrorCodes.FeatureNotSupported,
                $"refused with {refused.SqlState}"));
        Assert.Equal((OutsideTheRules, OutsideTheRules), (byOwner.SqlState, asReplica.SqlState));
    }

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
