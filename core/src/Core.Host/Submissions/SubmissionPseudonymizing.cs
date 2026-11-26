using Core.Ingestion;
using Core.Ingestion.Deciding;
using Core.Ingestion.Pseudonymizing;
using Core.Ingestion.Sanitizing;
using Core.Persistence.Content;

namespace Core.Host.Submissions;

/// <summary>
/// The pseudonymization of the items of a submission (issue 102, F3): the group of each mention is its referent, linked to the author whose group it shares; the conversation is swapped
/// in one draw, and each item of another form is a conversation of its own, with no marker of an item before it. The markers are held for the batch before the first item, drawn again
/// without the ones another batch holds, and each item is then written with its pseudonymized text, its map and its markers.
/// </summary>
internal static class SubmissionPseudonymizing
{
    /// <summary>The draws a conversation has before its writing fails, each one without the markers the ones before it met in another batch.</summary>
    internal const int Draws = 8;

    /// <summary>Swaps every sanitized text of the material, holds its markers for the batch and writes each unit as a pseudonymized item, in the order of the units.</summary>
    /// <param name="intake">Holds the swap and the writer.</param>
    /// <param name="kept">The domain and the batch, open, what the reader gave and the form it was declared in.</param>
    /// <param name="cleaned">The destinations decided, the position of each unit in the material and what the sanitization left of each unit.</param>
    /// <param name="cancellationToken">Cancels the writing.</param>
    /// <exception cref="InvalidOperationException">Every draw met a marker another batch holds, or a marker the batch held was taken from it.</exception>
    internal static async Task<List<(long Item, ReadUnit Unit)>> WriteAsync(SubmissionIntake intake, SubmissionStoring.Kept kept, Cleaned cleaned, CancellationToken cancellationToken)
    {
        var writer = intake.Writer!;
        var swapped = await SwappedAsync(
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

    /// <summary>The swap of the items, drawn again without the markers another batch holds until the batch holds every marker drawn, for as many draws as <see cref="Draws"/>.</summary>
    /// <param name="swap">Writes the markers.</param>
    /// <param name="items">The request of each item, and whether they are the messages of one conversation.</param>
    /// <param name="reserve">Holds the markers for the batch, all or none, and names the ones another batch holds.</param>
    internal static async Task<IReadOnlyList<Pseudonymized>> SwappedAsync(
        PseudonymSwap swap, (IReadOnlyList<SwapRequest> Requests, bool Conversation) items, Reservation reserve)
    {
        var lost = new HashSet<string>(StringComparer.Ordinal);
        for (var draw = 0; draw < Draws; draw++)
        {
            var swapped = Swapped(swap, items, lost);
            var taken = await reserve([.. swapped.SelectMany(item => item.Entries).Select(entry => entry.Marker.Text).Distinct(StringComparer.Ordinal)]).ConfigureAwait(false);
            if (taken.Count == 0)
            {
                return swapped;
            }

            lost.UnionWith(taken);
        }

        throw new InvalidOperationException("Every draw of the conversation met a marker another batch holds.");
    }

    private static IReadOnlyList<Pseudonymized> Swapped(PseudonymSwap swap, (IReadOnlyList<SwapRequest> Requests, bool Conversation) items, ICollection<string> lost)
    {
        if (items.Conversation)
        {
            return swap.SwapConversation(items.Requests, marker => lost.Contains(marker.Text));
        }

        var given = new HashSet<string>(lost, StringComparer.Ordinal);
        var swapped = new List<Pseudonymized>(items.Requests.Count);
        foreach (var request in items.Requests)
        {
            var item = swap.Swap(request, marker => given.Contains(marker.Text));
            given.UnionWith(item.Entries.Select(entry => entry.Marker.Text));
            swapped.Add(item);
        }

        return swapped;
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

    /// <summary>Holds the markers given for the batch, all or none, and names the ones another batch holds, none when the batch won them all.</summary>
    internal delegate Task<IReadOnlyList<string>> Reservation(string[] markers);

    /// <summary>What the analysis and the sanitization left of the material, by unit.</summary>
    /// <param name="Decided">The destination and the group of every occurrence of every text, and the group of each author.</param>
    /// <param name="Positions">The position in the material of the text of each unit.</param>
    /// <param name="Sanitized">The sanitized text of each unit, in the order of the units.</param>
    internal sealed record Cleaned(DecidedMaterial Decided, IReadOnlyList<int> Positions, IReadOnlyList<SanitizedText> Sanitized);
}
