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

    /// <summary>
    /// The motive of a batch that was opened and whose closing met another submission at the same link of the chain, after the attempts the batches make;
    /// nothing of it is kept.
    /// </summary>
    internal const string Collision = "concurrent_submission";

    /// <summary>The rounds of the rejection that cleans a batch up, each with the attempts the batches make, while the chain keeps refusing it for a collision.</summary>
    internal const int RejectionRounds = 3;

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
            foreach (var (unit, declared) in kept.Material.Units.Zip(Accommodating.Of(kept.Material, kept.Form)))
            {
                var accommodation = declared is null ? null : new ItemAccommodation(declared.FormCode, declared.Codes);
                _ = await writer.AddItemTextAsync(kept.DomainId, kept.BatchId, new NewItemText(unit.Text, Accommodation: accommodation), cancellationToken).ConfigureAwait(false);
            }

            return true;
        }
        catch (Exception failure) when (failure is InvalidOperationException or NpgsqlException)
        {
            return false;
        }
    }

    /// <summary>
    /// Writes the items of a file that was not turned back and accepts the batch; when that fails, rejects it with no item and says why: a write that failed, or a collision of the closing.
    /// When not even the rejection is made, the batch is left open with its items: the log says so by the batch and the domain only, and the answer is a write that failed.
    /// </summary>
    /// <param name="batches">Accepts and rejects the batch.</param>
    /// <param name="writer">Writes the items.</param>
    /// <param name="kept">The domain and the batch, open, and what the reader gave.</param>
    /// <param name="logger">Says, by the batch and the domain only, that a batch could not be rejected.</param>
    /// <param name="cancellationToken">Cancels the writing.</param>
    internal static async Task<Keeping> KeepAsync(IngestionBatches batches, ContentWriter writer, Kept kept, ILogger logger, CancellationToken cancellationToken)
    {
        var closing = new BatchClosing(kept.DomainId, kept.BatchId, CustodyAuthor.System);
        var accepted = await StoreAsync(writer, kept, cancellationToken).ConfigureAwait(false) ? await batches.AcceptAsync(closing, cancellationToken).ConfigureAwait(false) : null;
        if (accepted?.Outcome == BatchOutcome.Accepted)
        {
            return Keeping.Kept;
        }

        return await CleanUpAsync(motive => batches.RejectAsync(closing, motive, cancellationToken), accepted?.Collided == true, kept, logger).ConfigureAwait(false);
    }

    /// <summary>
    /// Rejects the batch that could not be kept, and again for a few rounds while the chain refuses the rejection for a collision.
    /// When the last round is refused too, the batch is left open and the log says so by the batch, the domain and how the rejection ended, never by the material.
    /// </summary>
    /// <param name="reject">Rejects the batch for the motive it is given, with the attempts the batches make.</param>
    /// <param name="collided">Whether the batch could not be kept because its acceptance met another submission at the same link of the chain.</param>
    /// <param name="kept">The domain and the batch.</param>
    /// <param name="logger">Says that the batch was left open.</param>
    internal static async Task<Keeping> CleanUpAsync(Func<string, Task<BatchResult>> reject, bool collided, Kept kept, ILogger logger)
    {
        var motive = collided ? Collision : WriteFailed;
        var rejected = await reject(motive).ConfigureAwait(false);
        for (var round = 1; round < RejectionRounds && rejected.Collided; round++)
        {
            rejected = await reject(motive).ConfigureAwait(false);
        }

        if (rejected.Outcome == BatchOutcome.Rejected)
        {
            return collided ? Keeping.Collided : Keeping.WriteFailed;
        }

        logger.BatchLeftOpen(kept.BatchId, kept.DomainId, rejected.Outcome);
        return Keeping.WriteFailed;
    }

    /// <summary>The reason of a service with no key to seal with: whatever the file is, nothing of it is kept; none when there is a key.</summary>
    /// <param name="sealing">The path that seals; none when the key could not be read.</param>
    internal static SubmissionReason? Unsealed(ProtectedContent? sealing)
    {
        var none = new List<SubmissionFinding>();
        return sealing is null ? new SubmissionReason(KeyUnavailable, none) : null;
    }

    /// <summary>How the keeping of a file ended.</summary>
    internal enum Keeping
    {
        /// <summary>The items were written and the batch was accepted.</summary>
        Kept,

        /// <summary>
        /// Some item could not be written, or the batch could not be accepted for a reason other than a collision; the batch was rejected as <see cref="WriteFailed" />,
        /// or was left open when not even the rejection could be made.
        /// </summary>
        WriteFailed,

        /// <summary>
        /// The acceptance met another submission at the same link of the chain and gave up after the attempts; the batch was rejected as <see cref="Collision" />,
        /// so nothing of it is kept.
        /// </summary>
        Collided,
    }

    /// <summary>The batch an item is written to, and what is written there.</summary>
    /// <param name="DomainId">The domain.</param>
    /// <param name="BatchId">The batch.</param>
    /// <param name="Material">What the reader gave.</param>
    /// <param name="Form">The form the material is accommodated into, which says what each item declares it lost.</param>
    internal sealed record Kept(int DomainId, long BatchId, ReadMaterial Material, MaterialForm Form);
}
