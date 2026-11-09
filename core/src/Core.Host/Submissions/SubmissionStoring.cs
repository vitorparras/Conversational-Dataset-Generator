using Core.Ingestion;
using Core.Persistence.Content;
using Core.Persistence.Content.Sealing;
using Core.Persistence.Content.Writing;
using Core.Persistence.Custody;
using Core.Persistence.Ingestion;
using Npgsql;

namespace Core.Host.Submissions;

/// <summary>
/// The last step of the path of a file that was read and conferred: whether its domain can seal, checked before the batch opens so that nothing is recorded when it cannot,
/// and the writing of each unit as an item with its raw text sealed, which either keeps all of them or none.
/// </summary>
internal static class SubmissionStoring
{
    /// <summary>The motive of a batch that was opened and could not keep its items; the items written are removed with the rejection.</summary>
    internal const string WriteFailed = "write_failed";

    /// <summary>The motive of the batch registered as rejected when the key that seals the content could not be read.</summary>
    internal const string KeyUnavailable = "content_key_unavailable";

    /// <summary>Whether the raw text of an item of the domain can be sealed now: there is a key to seal with, under a label loaded, registered and not revoked.</summary>
    /// <param name="sealing">The path that seals.</param>
    /// <param name="domainId">The domain of the batch.</param>
    internal static bool CanSeal(ProtectedContent? sealing, int domainId)
    {
        if (sealing is null)
        {
            return false;
        }

        try
        {
            using var key = sealing.Begin(ContentColumns.ItemKey, domainId);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>Writes each unit of the material as an item of the batch; false when some item could not be written, the ones before it still standing for the rejection to remove.</summary>
    /// <param name="writer">Writes the items.</param>
    /// <param name="kept">The domain and the batch, open, and what the reader gave.</param>
    /// <param name="cancellationToken">Cancels the writing.</param>
    internal static async Task<bool> StoreAsync(ContentWriter writer, Kept kept, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var unit in kept.Material.Units)
            {
                _ = await writer.AddItemTextAsync(kept.DomainId, kept.BatchId, new NewItemText(unit.Text), cancellationToken).ConfigureAwait(false);
            }

            return true;
        }
        catch (Exception failure) when (failure is InvalidOperationException or NpgsqlException)
        {
            return false;
        }
    }

    /// <summary>
    /// Writes the items of a file that was not turned back and accepts the batch; when that fails, rejects it with no item, and says why: null when the batch was accepted,
    /// the repeat when another batch accepted the same file first, which the database decides at the acceptance, and the failure to write otherwise.
    /// </summary>
    /// <param name="batches">Accepts and rejects the batch.</param>
    /// <param name="writer">Writes the items.</param>
    /// <param name="kept">The domain and the batch, open, and what the reader gave.</param>
    /// <param name="cancellationToken">Cancels the writing.</param>
    internal static async Task<string?> KeepAsync(IngestionBatches batches, ContentWriter writer, Kept kept, CancellationToken cancellationToken)
    {
        var closing = new BatchClosing(kept.DomainId, kept.BatchId, CustodyAuthor.System);
        var motive = WriteFailed;
        if (await StoreAsync(writer, kept, cancellationToken).ConfigureAwait(false))
        {
            var accepted = await batches.AcceptAsync(closing, cancellationToken).ConfigureAwait(false);
            if (accepted.Outcome == BatchOutcome.Accepted)
            {
                return null;
            }

            motive = accepted.Outcome == BatchOutcome.AlreadyReceived ? ReasonCodes.AlreadyReceived : WriteFailed;
        }

        _ = await batches.RejectAsync(closing, motive, cancellationToken).ConfigureAwait(false);
        return motive;
    }

    /// <summary>The reason of a service with no key to seal with: whatever the file is, nothing of it is kept; none when there is a key.</summary>
    /// <param name="sealing">The path that seals; none when the key could not be read.</param>
    internal static SubmissionReason? Unsealed(ProtectedContent? sealing) => sealing is null ? new SubmissionReason(KeyUnavailable, Array.Empty<SubmissionFinding>()) : null;

    /// <summary>The batch an item is written to, and what is written there.</summary>
    /// <param name="DomainId">The domain.</param>
    /// <param name="BatchId">The batch.</param>
    /// <param name="Material">What the reader gave.</param>
    internal sealed record Kept(int DomainId, long BatchId, ReadMaterial Material);
}
