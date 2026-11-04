using Core.Persistence.Custody.Chain;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Custody.Verification;

/// <summary>
/// Makes the seal of the chain and records it as an event of the chain: the tip is read and the event is added in the one serializable transaction, so that the seal is of the very event
/// the record points at, and a writer that reaches the same link first is told so and nothing is written. The seal leaves in the answer; the event keeps the count, and the digest is the chain's own.
/// </summary>
public sealed class ChainSealer
{
    private const string ReadTip = "SELECT sequence, digest FROM public.custody_event ORDER BY sequence DESC LIMIT 1";
    private const string ReadDigestAt = "SELECT digest FROM public.custody_event WHERE sequence = @sequence";

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

    /// <summary>Records a seal that was made before, and sent out: the event points at that seal, whatever the chain grew by since, and only if the digest of the event sealed is the one the seal carries.</summary>
    /// <param name="seal">The seal that was made and sent.</param>
    /// <param name="author">The system, or the active person who confirmed the record.</param>
    /// <param name="cancellationToken">Cancels the record.</param>
    /// <exception cref="SealDoesNotMatchException">The chain has no event at the sequence the seal points at, or its digest is not the seal's; nothing is written.</exception>
    public Task<SealRecording> RecordAsync(ChainSeal seal, CustodyAuthor author, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(seal);
        ArgumentNullException.ThrowIfNull(author);
        return RecordGivenAsync(seal, author, cancellationToken);
    }

    /// <summary>Seals the chain as it stands and records nothing, which is what is sent out before the record is made.</summary>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public Task<ChainSeal> MakeAsync(CancellationToken cancellationToken) => ReadSealAsync(dataSource.CreateCommand(ReadTip), cancellationToken);

    private async Task<SealRecording> RecordSealAsync(CustodyAuthor author, CancellationToken cancellationToken)
    {
        ChainSeal? made = null;
        var recorded = (await CustodyTransaction.AppendAfterAsync(
            dataSource,
            async scope =>
            {
                made = await ReadSealAsync(scope.CreateCommand(ReadTip), cancellationToken).ConfigureAwait(false);
                return CustodyEvent.SealRecorded(author, made.Events);
            },
            cancellationToken).ConfigureAwait(false))!;
        return new SealRecording { Recorded = recorded, Seal = recorded.Outcome == CustodyOutcome.Appended ? made : null };
    }

    private async Task<SealRecording> RecordGivenAsync(ChainSeal seal, CustodyAuthor author, CancellationToken cancellationToken)
    {
        var recorded = await CustodyTransaction.AppendAfterAsync(
            dataSource,
            async scope => await HoldsAsync(scope, seal, cancellationToken).ConfigureAwait(false) ? CustodyEvent.SealRecorded(author, seal.Events) : null,
            cancellationToken).ConfigureAwait(false);
        return recorded is null
            ? throw new SealDoesNotMatchException()
            : new SealRecording { Recorded = recorded, Seal = recorded.Outcome == CustodyOutcome.Appended ? seal : null };
    }

    private static async Task<bool> HoldsAsync(CustodyScope scope, ChainSeal seal, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(ReadDigestAt);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.AddWithValue("sequence", NpgsqlDbType.Bigint, seal.Events);
            var found = seal.Events == 0 ? ChainDigest.Origin() : await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return found is byte[] digest && string.Equals(Convert.ToHexStringLower(digest), seal.Digest, StringComparison.Ordinal);
        }
    }

    private async Task<ChainSeal> ReadSealAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
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
