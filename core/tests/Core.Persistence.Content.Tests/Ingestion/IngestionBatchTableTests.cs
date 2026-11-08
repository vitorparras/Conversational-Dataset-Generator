using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Content;
using Core.Persistence.Tests.Content;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// The table of the batches, judged by the database with direct statements: how a batch opens and closes, and what it refuses whoever asks,
/// the owner and a session that skips the ordinary triggers included
/// (issue 74, criteria 4 and 6).
/// </summary>
public sealed class IngestionBatchTableTests
{
    private const string WithoutEvent = "CE005";
    private const string OutsideTheRules = "CE006";
    private const string Opening =
        "WITH b AS (INSERT INTO content.ingestion_batch (domain_id, opened_by, condition) VALUES (1, 1, 'restrita') RETURNING id) "
        + "INSERT INTO public.custody_event (actor_person_id, item_kind, item_id, step, reason) SELECT 1, 'ingestion_batch', id, 'batch_opened', 'administrative_act' FROM b RETURNING item_id";
    private const string OpeningByAnotherPerson =
        "WITH b AS (INSERT INTO content.ingestion_batch (domain_id, opened_by, condition) VALUES (1, 1, 'restrita') RETURNING id) "
        + "INSERT INTO public.custody_event (actor_person_id, item_kind, item_id, step, reason) SELECT 3, 'ingestion_batch', id, 'batch_opened', 'administrative_act' FROM b RETURNING item_id";
    private const string AcceptedEvent =
        "INSERT INTO public.custody_event (actor_system, item_kind, item_id, step, reason) VALUES (true, 'ingestion_batch', @event, 'batch_closed', 'batch_accepted')";
    private const string RejectedEvent =
        "INSERT INTO public.custody_event (actor_system, item_kind, item_id, step, reason, reference) VALUES (true, 'ingestion_batch', @event, 'batch_closed', 'batch_rejected', 'invalid_rows')";
    private const string Accept = "UPDATE content.ingestion_batch SET state = 'accepted' WHERE id = @id";
    private const string Reject = "UPDATE content.ingestion_batch SET state = 'rejected', motive = 'invalid_rows' WHERE id = @id";
    private const string Reopen = "UPDATE content.ingestion_batch SET state = 'open', motive = NULL, closed_at = NULL WHERE id = @id";
    private const string AcceptRejected = "UPDATE content.ingestion_batch SET state = 'accepted', motive = NULL WHERE id = @id";
    private const string RemoveBatch = "DELETE FROM content.ingestion_batch WHERE id = @id";
    private const string EmptyBatches = "TRUNCATE content.ingestion_batch";
    private const string Events = "SELECT string_agg(step || ':' || reason || ':' || coalesce(reference, '-'), ',' ORDER BY sequence) FROM public.custody_event";
    private const string Held = "SELECT state || ':' || coalesce(motive, '-') || ':' || (closed_at IS NOT NULL) FROM content.ingestion_batch WHERE id = @id";

    [DatabaseFact]
    public async Task BatchOpensOpenWithTheEventOfItsOpeningAndTheMomentsAreTheServers()
    {
        await using var world = await IngestionWorld.CreateAsync();

        var batch = await OpenAsync(world);

        Assert.Equal(("open:-:false", "batch_opened:administrative_act:-"), (await HeldAsync(world, batch), await world.ScalarAsync(Events) as string));
    }

    [DatabaseFact]
    public async Task OpeningWithoutItsEventInTheSameTransactionIsRefusedWhenTheTransactionEnds()
    {
        await using var world = await IngestionWorld.CreateAsync();

        var refused = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var transaction = await ContentTransaction.BeginAsync(world.DataSource, 1, TestContext.Current.CancellationToken);
            await using var insert = transaction.CreateCommand("INSERT INTO content.ingestion_batch (domain_id, opened_by, condition) VALUES (1, 1)");
            _ = await insert.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        });

        Assert.Equal((WithoutEvent, "0"), (refused.SqlState, await world.ScalarAsync("SELECT count(*)::text FROM content.ingestion_batch") as string));
    }

    [DatabaseFact]
    public async Task OpeningWithTheEventOfAnotherPersonIsRefused()
    {
        await using var world = await IngestionWorld.CreateAsync();

        var refused = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, OpeningByAnotherPerson));

        Assert.Equal(WithoutEvent, refused.SqlState);
    }

    [DatabaseFact]
    public async Task ApplicationCannotWriteTheStateOrTheMomentsWhenOpening()
    {
        await using var world = await IngestionWorld.CreateAsync();

        var state = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, "INSERT INTO content.ingestion_batch (domain_id, opened_by, condition, state) VALUES (1, 1, 'restrita', 'accepted')"));
        var moment = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, "INSERT INTO content.ingestion_batch (domain_id, opened_by, condition, opened_at) VALUES (1, 1, 'restrita', now())"));

        Assert.Equal((PostgresErrorCodes.InsufficientPrivilege, PostgresErrorCodes.InsufficientPrivilege), (state.SqlState, moment.SqlState));
    }

    [DatabaseFact]
    public async Task BatchBornClosedIsRefusedToTheOwnerToo()
    {
        await using var world = await IngestionWorld.CreateAsync();

        var refused = await Assert.ThrowsAsync<PostgresException>(
            () => world.OwnerInDomainAsync(1, "INSERT INTO content.ingestion_batch (domain_id, opened_by, condition, state, closed_at) VALUES (1, 1, 'restrita', 'accepted', now())"));

        Assert.Equal(OutsideTheRules, refused.SqlState);
    }

    [DatabaseFact]
    public async Task BatchClosesAcceptedOrRejectedWithTheEventOfTheClosingAndTheMotiveIsTheReferenceOfTheEvent()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var accepted = await OpenAsync(world);
        var rejected = await OpenAsync(world);

        _ = await world.InDomainAfterAsync(AcceptedEvent, 1, Accept, ("event", accepted), ("id", accepted));
        _ = await world.InDomainAfterAsync(RejectedEvent, 1, Reject, ("event", rejected), ("id", rejected));

        Assert.Equal(
            ("accepted:-:true", "rejected:invalid_rows:true"),
            (await HeldAsync(world, accepted), await HeldAsync(world, rejected)));
    }

    [DatabaseFact]
    public async Task ClosingWithoutTheEventOfTheClosingInTheSameTransactionIsRefused()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await OpenAsync(world);

        var accepted = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, Accept, ("id", batch)));
        var rejected = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, Reject, ("id", batch)));
        var byOwner = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, Accept, ("id", batch)));

        Assert.Equal((WithoutEvent, WithoutEvent, WithoutEvent), (accepted.SqlState, rejected.SqlState, byOwner.SqlState));
    }

    [DatabaseFact]
    public async Task ClosingWithTheEventOfAnotherOutcomeOrAnotherBatchIsRefused()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await OpenAsync(world);
        var other = await OpenAsync(world);

        var ofOther = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAfterAsync(AcceptedEvent, 1, Accept, ("event", batch), ("id", other)));
        var ofAnotherOutcome = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAfterAsync(AcceptedEvent, 1, Reject, ("event", batch), ("id", batch)));

        Assert.Equal((WithoutEvent, WithoutEvent), (ofOther.SqlState, ofAnotherOutcome.SqlState));
    }

    [DatabaseFact]
    public async Task MotiveThatIsNotANameInSnakeCaseIsRefusedByTheChainAndByTheBatchWithoutAnEventThatSaysIt()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await OpenAsync(world);
        const string BadMotive = "UPDATE content.ingestion_batch SET state = 'rejected', motive = 'Invalid_rows' WHERE id = @id";

        var inTheChain = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, RejectedEventWith("Invalid_rows"), ("event", batch)));
        var inTheTable = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, BadMotive, ("id", batch)));

        Assert.Equal((PostgresErrorCodes.CheckViolation, WithoutEvent), (inTheChain.SqlState, inTheTable.SqlState));
    }

    [DatabaseFact]
    public async Task BatchThatClosedPassesToNothingElseNotEvenForTheOwnerOrAReplicaSession()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var accepted = await OpenAsync(world);
        _ = await world.InDomainAfterAsync(AcceptedEvent, 1, Accept, ("event", accepted), ("id", accepted));
        var rejected = await OpenAsync(world);
        _ = await world.InDomainAfterAsync(RejectedEvent, 1, Reject, ("event", rejected), ("id", rejected));
        var open = await OpenAsync(world);

        var byOwner = new[]
        {
            await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, Reopen, ("id", accepted))),
            await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, Reopen, ("id", rejected))),
            await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, AcceptRejected, ("id", rejected))),
            await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, Reject, ("id", accepted))),
            await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, Reopen, ("id", open))),
        };
        var asReplica = new[]
        {
            await Assert.ThrowsAsync<PostgresException>(() => AsReplicaAsync(world, Reopen, accepted)),
            await Assert.ThrowsAsync<PostgresException>(() => AsReplicaAsync(world, AcceptRejected, rejected)),
            await Assert.ThrowsAsync<PostgresException>(() => AsReplicaAsync(world, Reject, accepted)),
        };

        Assert.All(byOwner.Concat(asReplica), refused => Assert.Equal(OutsideTheRules, refused.SqlState));
    }

    [DatabaseFact]
    public async Task BatchIsNeverRemovedAndTheTableIsNeverEmptiedWhoeverAsks()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await OpenAsync(world);
        _ = await world.InDomainAfterAsync(RejectedEvent, 1, Reject, ("event", batch), ("id", batch));

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

    private static string RejectedEventWith(string motive) => RejectedEvent.Replace("'invalid_rows'", "'" + motive + "'", StringComparison.Ordinal);

    private static async Task<long> OpenAsync(ContentWorld world) => (long)(await world.InDomainAsync(1, Opening))!;

    private static async Task<string?> HeldAsync(ContentWorld world, long batch) => await world.InDomainAsync(1, Held, ("id", batch)) as string;

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
