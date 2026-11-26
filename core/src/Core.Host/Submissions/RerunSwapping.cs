using System.Globalization;
using Core.Boundary.Contracts;
using Core.Ingestion.Pseudonymizing;
using Core.Ingestion.Sanitizing;
using Core.Persistence.Content;
using Core.Persistence.Content.Rerun;
using Core.Persistence.Content.Writing;

namespace Core.Host.Submissions;

/// <summary>
/// The swap of an item rerun (issue 102, F3): the new sanitized text is pseudonymized again with the map the item holds, each mention matched by its original to the referent of the marker
/// it had, so that the item keeps its markers (decision 0057). Each marker of the map is one referent, the author's when an entry of it stands for an author; a mention whose original the
/// map does not hold is a referent of its own, drawn without the markers another batch holds.
/// </summary>
internal static class RerunSwapping
{
    /// <summary>The pseudonymized text and the entries of the new map of the item, as the swap makes them of the new sanitized text with the markers of the map it holds.</summary>
    /// <param name="swap">Writes the markers.</param>
    /// <param name="held">The item, its map and the markers another batch holds.</param>
    /// <param name="clean">The new sanitized text and the occurrences to pseudonymize in it.</param>
    internal static (string Text, IReadOnlyList<NewMapEntry> Map) Again(PseudonymSwap swap, RerunSource held, SanitizedText clean)
    {
        var map = held.Map ?? throw new ArgumentException("The item rerun holds no map.", nameof(held));
        var author = map.FirstOrDefault(entry => entry.Author)?.Original;
        var byMarker = ReferentsOf(map, author);
        var byOriginal = new Dictionary<string, Referent>(StringComparer.Ordinal);
        foreach (var entry in map.OrderBy(entry => entry.Author && string.Equals(entry.Original, author, StringComparison.Ordinal)))
        {
            _ = byOriginal.TryAdd(entry.Original, byMarker[entry.Marker]);
        }

        var request = new SwapRequest
        {
            Sanitized = clean.Text,
            Occurrences = [.. clean.Pseudonyms.Select(occurrence => new ReferredOccurrence(occurrence, Matched(byOriginal, byMarker.Count, Span(clean.Text, occurrence.Occurrence))))],
            Author = author,
            ItemId = held.ItemId,
            Map = new ItemMap(held.ItemId, [.. map.Select(entry => new MapEntry(byMarker[entry.Marker], MarkerOf(entry.Marker), entry.Original))]),
        };
        var swapped = swap.Swap(request, marker => held.Taken.Contains(marker.Text));
        return (swapped.Text, [.. swapped.Entries.Select(entry => new NewMapEntry(entry.Marker.Text, entry.Original, entry.IsAuthor, OccurrenceDestination.Pseudonymize))]);
    }

    /// <summary>The referent of each marker of the map: the author of the message for its own, another author by an original of the marker, and a group for each of the rest.</summary>
    private static Dictionary<string, Referent> ReferentsOf(IReadOnlyList<NewMapEntry> map, string? author)
    {
        var referents = new Dictionary<string, Referent>(StringComparer.Ordinal);
        foreach (var entry in map.Where(entry => !referents.ContainsKey(entry.Marker)))
        {
            var authors = map.Where(other => other.Author && string.Equals(other.Marker, entry.Marker, StringComparison.Ordinal)).Select(other => other.Original).ToList();
            referents[entry.Marker] = authors.Count == 0 ? Referent.Group(referents.Count + 1) : Referent.Author(author is not null && authors.Contains(author) ? author : authors[0]);
        }

        return referents;
    }

    /// <summary>The referent of the original in the map, or a group of its own, which the same original met again shares.</summary>
    private static Referent Matched(Dictionary<string, Referent> byOriginal, int markers, string original)
    {
        if (!byOriginal.TryGetValue(original, out var referent))
        {
            referent = Referent.Group(markers + byOriginal.Count + 1);
            byOriginal.Add(original, referent);
        }

        return referent;
    }

    /// <summary>The span of the occurrence in the text, cut by code points as the swap cuts it.</summary>
    private static string Span(string text, Occurrence occurrence) =>
        string.Concat(text.EnumerateRunes().Skip(occurrence.Start).Take(occurrence.End - occurrence.Start).Select(rune => rune.ToString()));

    private static Marker MarkerOf(string text)
    {
        var kind = Enum.GetValues<MarkerKind>().First(kind => text.StartsWith(MarkerKinds.Prefix(kind) + "_", StringComparison.Ordinal));
        return new Marker(kind, int.Parse(text.AsSpan(MarkerKinds.Prefix(kind).Length + 1), NumberStyles.None, CultureInfo.InvariantCulture));
    }
}
