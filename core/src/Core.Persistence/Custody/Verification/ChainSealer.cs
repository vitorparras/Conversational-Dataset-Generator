using Core.Persistence.Custody.Chain;
using Npgsql;

namespace Core.Persistence.Custody.Verification;

/// <summary>
/// Makes the seal of the chain and records it as an event of the chain: the tip is read and the event is added in the one serializable transaction, so that the seal is of the very event
/// the record points at, and a writer that reaches the same link first is told so and nothing is written. The seal leaves in the answer; the event keeps the count, and the digest is the chain's own.
/// </summary>
public sealed class ChainSealer
{
    private const string ReadTip = "SELECT sequence, digest FROM public.custody_event ORDER BY sequence DESC LIMIT 1";

    private readonly NpgsqlDataSource dataSource;
    private readonly TimeProvider time;

    /// <summary>The sealer reads and records through the data source built with the application's credential.</summary>
    /// <param name="dataSource">The source of the connections.</param>
    /// <param name="time">Tells the moment of the seal.</param>
    public ChainSealer(NpgsqlDataSource dataSource, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(time);
        this.dataSource = dataSource;
        this.time = time;
    }

    /// <summary>Seals the chain as it stands and records it; the chain refuses it while the last verification failed, and the answer says so.</summary>
    /// <param name="author">The system, or the active person who confirmed the record.</param>
    /// <param name="cancellationToken">Cancels the record.</param>
    public Task<SealRecording> RecordAsync(CustodyAuthor author, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(author);
        return RecordSealAsync(author, cancellationToken);
    }

    private async Task<SealRecording> RecordSealAsync(CustodyAuthor author, CancellationToken cancellationToken)
    {
        ChainSeal? made = null;
        var recorded = (await CustodyTransaction.AppendAfterAsync(
            dataSource,
            async scope =>
            {
                made = await SealAsync(scope, cancellationToken).ConfigureAwait(false);
                return CustodyEvent.SealRecorded(author, made.Events);
            },
            cancellationToken).ConfigureAwait(false))!;
        return new SealRecording { Recorded = recorded, Seal = recorded.Outcome == CustodyOutcome.Appended ? made : null };
    }

    private async Task<ChainSeal> SealAsync(CustodyScope scope, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(ReadTip);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
                    ? ChainSeal.Of(time.GetUtcNow(), reader.GetInt64(0), (byte[])reader["digest"])
                    : ChainSeal.Of(time.GetUtcNow(), 0, ChainDigest.Origin());
            }
        }
    }
}
