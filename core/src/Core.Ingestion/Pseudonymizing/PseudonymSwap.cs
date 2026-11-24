using System.Text;
using Core.Boundary.Contracts;
using Core.Ingestion.Deciding;

namespace Core.Ingestion.Pseudonymizing;

/// <summary>
/// The swap of one item (decision 0012): the marker of each referent is written in place of its span of the sanitized text, and the map of the item keeps marker and original. The
/// conversation is resolved in the item itself (decision 0057): the only map taken is the one of the item, whose markers are kept, and a referent it does not have gets a drawn one.
/// Two spans that overlap become one, under the referent of the one that starts first; a span ending where another starts is no overlap. A span whose destination is not to
/// pseudonymize never becomes an entry: the swap stops before anything is written.
/// </summary>
public sealed class PseudonymSwap
{
    private readonly ITextBoundaries boundaries;
    private readonly MarkerDraw draw;

    /// <summary>The swap that cuts the text by code points and draws the markers a referent does not have yet.</summary>
    /// <param name="boundaries">Counts and cuts the text by code points.</param>
    /// <param name="draw">Draws the number of a new marker.</param>
    public PseudonymSwap(ITextBoundaries boundaries, MarkerDraw draw)
    {
        this.boundaries = boundaries ?? throw new ArgumentNullException(nameof(boundaries));
        this.draw = draw ?? throw new ArgumentNullException(nameof(draw));
    }

    /// <summary>The pseudonymized text and the entries of the map of the item.</summary>
    /// <param name="request">The sanitized text, its occurrences and, when the item is pseudonymized again, the item and its map.</param>
    /// <param name="inUseElsewhere">Whether a marker is held by another conversation, of any domain.</param>
    /// <exception cref="ArgumentException">The map is not the one of the item, or an occurrence falls outside the text or names nothing a marker stands for.</exception>
    /// <exception cref="PseudonymOfRemovedException">An occurrence is not to pseudonymize.</exception>
    public Pseudonymized Swap(SwapRequest request, Func<Marker, bool> inUseElsewhere)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(inUseElsewhere);
        if (request.Map is { } map && map.ItemId != request.ItemId)
        {
            throw new ArgumentException("The swap takes no map but the one of its item.", nameof(request));
        }

        if (request.Occurrences.Any(occurrence => occurrence.Decided.Destination != Destination.Pseudonymize))
        {
            throw new PseudonymOfRemovedException();
        }

        var markers = new ConversationMarkers(draw, inUseElsewhere, Known(request.Map));
        return Written(request.Sanitized, Merged(request.Occurrences, boundaries.CodePointCount(request.Sanitized)), markers);
    }

    private static Dictionary<Referent, Marker> Known(ItemMap? map)
    {
        var known = new Dictionary<Referent, Marker>();
        foreach (var entry in map?.Entries ?? [])
        {
            _ = known.TryAdd(entry.Referent, entry.Marker);
        }

        return known;
    }

    private static List<Span> Merged(IReadOnlyList<ReferredOccurrence> occurrences, int length)
    {
        var merged = new List<Span>(occurrences.Count);
        foreach (var occurrence in occurrences.OrderBy(occurrence => occurrence.Decided.Occurrence.Start).ThenByDescending(occurrence => occurrence.Decided.Occurrence.End))
        {
            var found = occurrence.Decided.Occurrence;
            if (found.Start < 0 || found.End <= found.Start || found.End > length)
            {
                throw new ArgumentException("An occurrence falls outside the text.", nameof(occurrences));
            }

            var kind = MarkerKinds.Of(found.Kind) ?? throw new ArgumentException("An occurrence names nothing a marker stands for.", nameof(occurrences));
            if (merged.Count > 0 && found.Start < merged[^1].End)
            {
                merged[^1] = merged[^1] with { End = Math.Max(merged[^1].End, found.End) };
                continue;
            }

            merged.Add(new Span(found.Start, found.End, kind, occurrence.Referent));
        }

        return merged;
    }

    private Pseudonymized Written(string text, IReadOnlyCollection<Span> spans, ConversationMarkers markers)
    {
        var written = new StringBuilder(text.Length);
        var entries = new List<MapEntry>(spans.Count);
        var at = 0;
        foreach (var span in spans)
        {
            var marker = markers.Of(span.Referent, span.Kind);
            var entry = new MapEntry(span.Referent, marker, boundaries.Span(text, span.Start, span.End));
            if (!entries.Contains(entry))
            {
                entries.Add(entry);
            }

            _ = written.Append(boundaries.Span(text, at, span.Start)).Append(marker.Text);
            at = span.End;
        }

        _ = written.Append(boundaries.Span(text, at, boundaries.CodePointCount(text)));
        return new Pseudonymized(written.ToString(), entries);
    }

    private sealed record Span(int Start, int End, MarkerKind Kind, Referent Referent);
}
