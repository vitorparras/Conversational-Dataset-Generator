using Core.Ingestion.Deciding;
using Core.Ingestion.Sanitizing;
using Core.Persistence.Custody;
using Core.Persistence.Ingestion;
using Npgsql;

namespace Core.Host.Submissions;

/// <summary>
/// The sanitization of every text of the material, in its order, before the first item is written (issues 87 and 93): either every text has its sanitized form or none is written.
/// A step that fails is told by the batch, the domain, the step and the position of the item, never by a character of the text nor by the message of the failure.
/// </summary>
internal static class SubmissionSanitizing
{
    /// <summary>The sanitized form of each text, in the order of the units; none when a step failed, which the log then says.</summary>
    /// <param name="sanitizer">Runs the declared steps over a text.</param>
    /// <param name="decided">The material with the destination decided for every occurrence of every text.</param>
    /// <param name="kept">The domain, the batch and what the reader gave.</param>
    /// <param name="positions">The position in the material of the text of each unit.</param>
    /// <param name="logger">Says which step failed, and at which item.</param>
    /// <exception cref="InvalidOperationException">The sanitizer refused a text whose destinations were decided.</exception>
    internal static IReadOnlyList<SanitizedText>? Of(Sanitizer sanitizer, DecidedMaterial decided, SubmissionStoring.Kept kept, IReadOnlyCollection<int> positions, ILogger logger)
    {
        var sanitized = new List<SanitizedText>(positions.Count);
        foreach (var (unit, position) in kept.Material.Units.Zip(positions))
        {
            try
            {
                sanitized.Add(sanitizer.Sanitize(unit.Text, decided.Of(position)).Sanitized
                    ?? throw new InvalidOperationException("The sanitizer refused a text whose destinations were decided."));
            }
            catch (SanitizationStepFailedException failed)
            {
                logger.SanitizationFailed(kept.BatchId, kept.DomainId, failed.Step, sanitized.Count, failed.InnerException?.GetType().Name ?? nameof(SanitizationStepFailedException));
                return null;
            }
        }

        return sanitized;
    }

    /// <summary>
    /// Records in the chain that the batch was sanitized with the version of the steps, by whoever submitted it, once its items are written; a record the chain refused, or one that failed,
    /// is a write that failed, so that no batch keeps items without the version of its sanitization.
    /// </summary>
    /// <param name="batches">Records the event of the batch.</param>
    /// <param name="sanitizer">Holds the version of the steps every item carries.</param>
    /// <param name="kept">The domain, the batch and who submitted it.</param>
    /// <param name="cancellationToken">Cancels the record.</param>
    internal static async Task<SubmissionStoring.Keeping> RecordedAsync(IngestionBatches batches, Sanitizer sanitizer, SubmissionStoring.Kept kept, CancellationToken cancellationToken)
    {
        try
        {
            var batch = new BatchClosing(kept.DomainId, kept.BatchId, CustodyAuthor.Person(kept.AuthorId));
            var recorded = await batches.SanitizedAsync(batch, sanitizer.Version, cancellationToken).ConfigureAwait(false);
            return recorded?.Outcome == CustodyOutcome.Appended ? SubmissionStoring.Keeping.Kept : SubmissionStoring.Keeping.WriteFailed;
        }
        catch (OperationCanceledException)
        {
            return SubmissionStoring.Keeping.Cancelled;
        }
        catch (Exception failure) when (failure is InvalidOperationException or NpgsqlException)
        {
            return SubmissionStoring.Keeping.WriteFailed;
        }
    }
}
