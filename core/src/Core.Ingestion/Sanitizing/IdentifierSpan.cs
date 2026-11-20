namespace Core.Ingestion.Sanitizing;

/// <summary>A span of the text, in code points, with the marker that replaces it; no marker when it is an occurrence that leaves only if it touches an identifier.</summary>
/// <param name="Start">The code point offset where the span starts.</param>
/// <param name="End">The code point offset just past the span.</param>
/// <param name="Marker">The marker of the identifier, or null for an occurrence.</param>
internal sealed record IdentifierSpan(int Start, int End, SanitizationMarker? Marker)
{
    /// <summary>
    /// The spans that overlap one another joined into one, each kept only when it holds an identifier, and written with the marker of the first identifier in it, so that an
    /// identifier found twice, or an occurrence that touches it, wholly or in part, leaves once and under one marker.
    /// </summary>
    internal static List<IdentifierSpan> Joined(IEnumerable<IdentifierSpan> spans)
    {
        var joined = new List<IdentifierSpan>();
        IdentifierSpan? current = null;
        foreach (var span in spans.OrderBy(span => span.Start).ThenBy(span => span.Marker is null).ThenByDescending(span => span.End))
        {
            if (current is not null && span.Start < current.End)
            {
                current = current with { End = Math.Max(current.End, span.End), Marker = current.Marker ?? span.Marker };
                continue;
            }

            joined.AddRange(Kept(current));
            current = span;
        }

        joined.AddRange(Kept(current));
        return joined;
    }

    private static IdentifierSpan[] Kept(IdentifierSpan? span) => span?.Marker is null ? [] : [span];
}
