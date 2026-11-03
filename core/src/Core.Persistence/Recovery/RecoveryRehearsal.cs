using System.Security.Cryptography;
using Core.Persistence.Content;
using Core.Persistence.Content.Sealing;
using Npgsql;

namespace Core.Persistence.Recovery;

/// <summary>
/// The rehearsal of the recovery over the copy of the database (decision 0014): it opens in memory a sample of the raw text and of the reversal map of every domain, with the key and the labels of the
/// copy of safekeeping, and counts what opened and what did not. What was opened is dropped at once: nothing is shown, and no reading is recorded in the chain of the copy.
/// </summary>
public sealed class RecoveryRehearsal
{
    private const string Tip = "SELECT sequence, encode(digest, 'hex') FROM public.custody_event ORDER BY sequence DESC LIMIT 1";
    private const string DomainIds = "SELECT id FROM public.domain ORDER BY id";
    private const string RawSamples = "SELECT raw_text, dek_epoch, dek_wrapped FROM content.item_text WHERE raw_text IS NOT NULL AND emptied_at IS NULL ORDER BY id LIMIT 3";
    private const string MapSamples = "SELECT entries, dek_epoch, dek_wrapped FROM content.reversal_map WHERE entries IS NOT NULL AND emptied_at IS NULL ORDER BY id LIMIT 3";

    private readonly NpgsqlDataSource dataSource;
    private readonly ProtectedContent content;
    private readonly TimeProvider time;

    /// <summary>The rehearsal reads the copy through the source, opens with the path of protected content given, and tells the moment by the clock.</summary>
    public RecoveryRehearsal(NpgsqlDataSource dataSource, ProtectedContent content, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(time);
        this.dataSource = dataSource;
        this.content = content;
        this.time = time;
    }

    /// <summary>Opens the samples and gives the receipt; a copy with no chain is not a copy of the production and is refused.</summary>
    /// <param name="keyProof">The proof of the key the content opens with, as <see cref="KeyProof.Of" /> gives it.</param>
    /// <param name="cancellationToken">Cancels the rehearsal.</param>
    public async Task<RecoveryReceipt> RehearseAsync(string keyProof, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(keyProof);
        var (sequence, digest) = await TipAsync(cancellationToken).ConfigureAwait(false);
        var total = new SampleCounts(0, 0, 0);
        foreach (var domain in await DomainsAsync(cancellationToken).ConfigureAwait(false))
        {
            var (rawOpened, rawFailed) = await OpenedAsync(domain, ContentColumns.RawText, RawSamples, cancellationToken).ConfigureAwait(false);
            var (mapsOpened, mapsFailed) = await OpenedAsync(domain, ContentColumns.Entries, MapSamples, cancellationToken).ConfigureAwait(false);
            total = new SampleCounts(total.Raw + rawOpened, total.Maps + mapsOpened, total.Failed + rawFailed + mapsFailed);
        }

        var moment = time.GetUtcNow();
        return new RecoveryReceipt(moment.AddTicks(-(moment.Ticks % TimeSpan.TicksPerSecond)), sequence, digest, keyProof, total);
    }

    private async Task<(int Opened, int Failed)> OpenedAsync(int domain, ProtectedColumn column, string statement, CancellationToken cancellationToken)
    {
        var (opened, failed) = (0, 0);
        var transaction = await ContentTransaction.BeginAsync(dataSource, domain, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var command = transaction.CreateCommand(statement);
            await using (command.ConfigureAwait(false))
            {
                var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        var wasOpened = TryOpen(domain, column, (byte[])reader.GetValue(0), new ContentKey(reader.GetInt32(1), (byte[])reader.GetValue(2)));
                        (opened, failed) = (opened + (wasOpened ? 1 : 0), failed + (wasOpened ? 0 : 1));
                    }
                }
            }
        }

        return (opened, failed);
    }

    private bool TryOpen(int domain, ProtectedColumn column, byte[] stored, ContentKey key)
    {
        try
        {
            _ = content.Open(column, domain, stored, key);
            return true;
        }
        catch (Exception refused) when (refused is Core.Platform.Keys.EnvelopeRefusedException or InvalidOperationException or CryptographicException or ArgumentException)
        {
            return false;
        }
    }

    private async Task<(long Sequence, string Digest)> TipAsync(CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(Tip);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
                    ? (reader.GetInt64(0), reader.GetString(1))
                    : throw new InvalidOperationException("a cópia não tem cadeia de custódia, e não é cópia da produção");
            }
        }
    }

    private async Task<List<int>> DomainsAsync(CancellationToken cancellationToken)
    {
        var ids = new List<int>();
        var command = dataSource.CreateCommand(DomainIds);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    ids.Add(reader.GetInt32(0));
                }
            }
        }

        return ids;
    }
}
