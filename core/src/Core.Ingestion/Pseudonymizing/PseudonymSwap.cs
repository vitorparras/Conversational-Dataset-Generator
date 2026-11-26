using System.Text;
using Core.Boundary.Contracts;
using Core.Ingestion.Deciding;

namespace Core.Ingestion.Pseudonymizing;

/// <summary>
/// The swap of the items of one conversation (decision 0012): the marker of each referent is written in place of its span of the sanitized text, and the map of each item keeps
/// marker and original. The conversation is resolved in itself, in one draw for all its messages (decision 0057): the only maps taken are the ones of the items swapped, whose
/// markers are kept, and a referent they do not have gets a drawn one; the author of each message gets an entry under its name.
/// Two spans that overlap become one, under the referent of the one that starts first; a span ending where another starts is no overlap. A span whose destination is not to
/// pseudonymize never becomes an entry: the swap stops before anything is written.
/// </summary>
public sealed class PseudonymSwap
{
    private readonly ITextBoundaries boundaries;
    private readonly ConversationOpening conversation;

    /// <summary>The swap that cuts the text by code points and draws, in the conversation of the item, the markers a referent does not have yet.</summary>
    /// <param name="boundaries">Counts and cuts the text by code points.</param>
    /// <param name="conversation">Opens the markers of the conversation of an item, with the draw the composition root gives it.</param>
    public PseudonymSwap(ITextBoundaries boundaries, ConversationOpening conversation)
    {
        this.boundaries = boundaries ?? throw new ArgumentNullException(nameof(boundaries));
        this.conversation = conversation ?? throw new ArgumentNullException(nameof(conversation));
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
        Check(request, nameof(request));
        return Swapped([request], inUseElsewhere)[0];
    }

    /// <summary>The pseudonymized text and the entries of the map of each item of one conversation, in the order given, in one draw: a referent has one marker in all of them.</summary>
    /// <param name="requests">The items of the conversation, each with its sanitized text, its occurrences and the author of its message.</param>
    /// <param name="inUseElsewhere">Whether a marker is held by another conversation, of any domain.</param>
    /// <exception cref="ArgumentException">A map is not the one of its item, or an occurrence falls outside its text or names nothing a marker stands for.</exception>
    /// <exception cref="PseudonymOfRemovedException">An occurrence is not to pseudonymize.</exception>
    public IReadOnlyList<Pseudonymized> SwapConversation(IReadOnlyList<SwapRequest> requests, Func<Marker, bool> inUseElsewhere)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(inUseElsewhere);
        foreach (var request in requests)
        {
            Check(request ?? throw new ArgumentException("A request of the conversation is missing.", nameof(requests)), nameof(requests));
        }

        return Swapped(requests, inUseElsewhere);
    }

    private static void Check(SwapRequest request, string parameter)
    {
        if (request.Map is { } map && map.ItemId != request.ItemId)
        {
            throw new ArgumentException("The swap takes no map but the one of its item.", parameter);
        }

        if (request.Occurrences.Any(occurrence => occurrence.Decided.Destination != Destination.Pseudonymize))
        {
            throw new PseudonymOfRemovedException();
        }
    }

    private List<Pseudonymized> Swapped(IReadOnlyList<SwapRequest> requests, Func<Marker, bool> inUseElsewhere)
    {
        var spans = requests.Select(request => Merged(request.Occurrences, boundaries.CodePointCount(request.Sanitized))).ToList();
        var markers = conversation(inUseElsewhere, Known(requests));
        return [.. requests.Select((request, index) => Written(request, spans[index], markers))];
    }

    private static Dictionary<Referent, Marker> Known(IEnumerable<SwapRequest> requests)
    {
        var known = new Dictionary<Referent, Marker>();
        foreach (var entry in requests.SelectMany(request => request.Map?.Entries ?? []))
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

    private Pseudonymized Written(SwapRequest request, IReadOnlyCollection<Span> spans, ConversationMarkers markers)
    {
        var text = request.Sanitized;
        var written = new StringBuilder(text.Length);
        var entries = new List<MapEntry>(spans.Count + 1);
        if (request.Author is { } author)
        {
            entries.Add(new MapEntry(Referent.Author(author), markers.Of(Referent.Author(author), MarkerKind.Person), author));
        }

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
