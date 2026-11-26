using Core.Ingestion;
using Core.Ingestion.Deciding;
using Core.Ingestion.Pseudonymizing;
using Core.Ingestion.Sanitizing;
using Core.Persistence.Content;

namespace Core.Host.Submissions;

/// <summary>
/// The pseudonymization of the items of a submission (issue 102, F3): the group of each mention is its referent, linked to the author whose group it shares; the conversation is swapped
/// in one draw (<see cref="ConversationDraws"/>), and each item is then written with its pseudonymized text, its map and its markers.
/// </summary>
internal static class SubmissionPseudonymizing
{
    /// <summary>Swaps every sanitized text of the material, holds its markers for the batch and writes each unit as a pseudonymized item, in the order of the units.</summary>
    /// <param name="intake">Holds the swap and the writer.</param>
    /// <param name="kept">The domain and the batch, open, what the reader gave and the form it was declared in.</param>
    /// <param name="cleaned">The destinations decided, the position of each unit in the material and what the sanitization left of each unit.</param>
    /// <param name="cancellationToken">Cancels the writing.</param>
    /// <exception cref="InvalidOperationException">Every draw met a marker another batch holds, or a marker the batch held was taken from it.</exception>
    internal static async Task<List<(long Item, ReadUnit Unit)>> WriteAsync(SubmissionIntake intake, SubmissionStoring.Kept kept, Cleaned cleaned, CancellationToken cancellationToken)
    {
        var writer = intake.Writer!;
        var swapped = await ConversationDraws.SwappedAsync(
            intake.Swap, (RequestsOf(kept.Accepted, cleaned), kept.Accepted is Conversation), markers => writer.ReserveMarkersAsync(kept.DomainId, kept.BatchId, markers, cancellationToken))
            .ConfigureAwait(false);
        var units = kept.Material.Units.Zip(Accommodating.Of(kept.Material, kept.Form), cleaned.Positions).Zip(cleaned.Sanitized, swapped);
        var written = new List<(long Item, ReadUnit Unit)>();
        foreach (var ((unit, declared, position), clean, pseudonymized) in units)
        {
            var accommodation = declared is null ? null : new ItemAccommodation(declared.FormCode, declared.Codes);
            var text = DecidedItemTexts.Swapped(cleaned.Decided, position, unit.Text, (clean, pseudonymized)) with { Accommodation = accommodation };
            var item = await writer.AddPseudonymizedItemAsync(kept.DomainId, kept.BatchId, text, cancellationToken).ConfigureAwait(false);
            written.Add((item.ItemId ?? throw new InvalidOperationException("A marker the batch held was taken from it."), unit));
        }

        return written;
    }

    /// <summary>The request of each unit: its sanitized text, each mention with its referent, and the author of its message.</summary>
    private static List<SwapRequest> RequestsOf(Material material, Cleaned cleaned)
    {
        var authors = MaterialTexts.AuthorsOf(material);
        return [.. cleaned.Positions.Zip(cleaned.Sanitized, (position, clean) => new SwapRequest
        {
            Sanitized = clean.Text,
            Occurrences = [.. clean.Pseudonyms.Select(occurrence => MentionReferents.Refer(occurrence, cleaned.Decided.AuthorGroups))],
            Author = authors[position],
        })];
    }

    /// <summary>What the analysis and the sanitization left of the material, by unit.</summary>
    /// <param name="Decided">The destination and the group of every occurrence of every text, and the group of each author.</param>
    /// <param name="Positions">The position in the material of the text of each unit.</param>
    /// <param name="Sanitized">The sanitized text of each unit, in the order of the units.</param>
    internal sealed record Cleaned(DecidedMaterial Decided, IReadOnlyList<int> Positions, IReadOnlyList<SanitizedText> Sanitized);
}
