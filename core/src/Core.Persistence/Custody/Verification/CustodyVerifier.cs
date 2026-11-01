using System.Data;
using Npgsql;

namespace Core.Persistence.Custody;

/// <summary>
/// Verifies the whole chain of custody on demand: reads every event in one consistent, read-only view, recomputes each digest here with its own encoding and not through
/// the function the database writes them with, tells the first event that does not hold, and records the verdict as an event of the chain written by the system.
/// While the last verdict recorded is a failure the chain accepts no event but a verification, and nothing here rebuilds a link.
/// </summary>
public sealed class CustodyVerifier
{
    // The moment of an event is kept in microseconds since 2000-01-01 UTC; a tick is a tenth of a microsecond.
    private const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
    private static readonly long EpochTicks = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero).UtcTicks;

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

    private static async Task JudgeRowsAsync(ChainJudge judge, NpgsqlDataReader reader, CancellationToken cancellationToken)
    {
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            judge.Accept(Stored(reader));
        }
    }

    private static StoredEvent Stored(NpgsqlDataReader row) => new(
        (long)row["sequence"],
        (row.GetFieldValue<DateTimeOffset>(row.GetOrdinal("occurred_at")).UtcTicks - EpochTicks) / TicksPerMicrosecond,
        Optional(row["actor_person_id"]),
        (bool)row["actor_system"],
        (string)row["item_kind"],
        (long)row["item_id"],
        (string)row["step"],
        (string)row["reason"],
        Optional(row["corrects"]),
        (byte[])row["previous_digest"],
        (byte[])row["digest"],
        (string)row["recorded_by"]);

    private static long? Optional(object value) => value is long present ? present : null;
}
