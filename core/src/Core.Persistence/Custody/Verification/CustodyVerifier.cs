using System.Buffers.Binary;
using System.Data;
using System.Data.Common;
using Npgsql;

namespace Core.Persistence.Custody.Verification;

/// <summary>
/// Verifies the whole chain of custody on demand: reads every event in one consistent, read-only view, recomputes each digest here with its own encoding and not through
/// the function the database writes them with, tells the first event that does not hold, and records the verdict as an event of the chain written by the system.
/// Nothing here rebuilds a link.
/// </summary>
public sealed class CustodyVerifier
{
    private readonly NpgsqlDataSource dataSource;
    private readonly TimeProvider time;
    private readonly CustodyChain chain;

    /// <summary>The verifier reads and records through the data source built with the application's credential.</summary>
    /// <param name="dataSource">The source of the connections.</param>
    /// <param name="time">Tells the moment of the view.</param>
    public CustodyVerifier(NpgsqlDataSource dataSource, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(time);
        this.dataSource = dataSource;
        this.time = time;
        chain = new CustodyChain(dataSource);
    }

    /// <summary>Verifies the chain and records the verdict; a verdict the chain did not accept (another writer was first, say) is told in the answer, and the verification may be repeated.</summary>
    /// <param name="cancellationToken">Cancels the verification.</param>
    public async Task<ChainVerification> VerifyAsync(CancellationToken cancellationToken)
    {
        var judge = new ChainJudge();
        var verifiedAt = await ReadAsync(judge, cancellationToken).ConfigureAwait(false);
        var recorded = await chain.AppendAsync(CustodyEvent.Verification(judge.Read, judge.First?.Sequence), cancellationToken).ConfigureAwait(false);
        return new ChainVerification
        {
            Scope = VerificationScope.Installation,
            EventsRead = judge.Read,
            VerifiedAt = verifiedAt,
            FirstInconsistent = judge.First,
            Recorded = recorded,
        };
    }

    private async Task<DateTimeOffset> ReadAsync(ChainJudge judge, CancellationToken cancellationToken)
    {
        var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                await ReadOnlyAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                var verifiedAt = time.GetUtcNow();
                await FeedAsync(judge, connection, transaction, cancellationToken).ConfigureAwait(false);
                return verifiedAt;
            }
        }
    }

    private static async Task ReadOnlyAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        var command = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction);
        await using (command.ConfigureAwait(false))
        {
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task FeedAsync(ChainJudge judge, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        var command = new NpgsqlCommand(CustodyStatements.ReadChain, connection, transaction);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                await JudgeRowsAsync(judge, reader, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task JudgeRowsAsync(ChainJudge judge, DbDataReader reader, CancellationToken cancellationToken)
    {
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            judge.Accept(new StoredEvent(
                (long)reader["sequence"],
                BinaryPrimitives.ReadInt64BigEndian((byte[])reader["occurred_at_send"]),
                Optional(reader["actor_person_id"]),
                (bool)reader["actor_system"],
                (string)reader["item_kind"],
                (long)reader["item_id"],
                (string)reader["step"],
                (string)reader["reason"],
                Optional(reader["corrects"]),
                (byte[])reader["previous_digest"],
                (byte[])reader["digest"],
                (string)reader["recorded_by"]));
        }
    }

    private static long? Optional(object value) => value is long present ? present : null;
}
