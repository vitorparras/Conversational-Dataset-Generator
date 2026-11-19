using Core.Boundary.Contracts;

namespace Core.Boundary.Text;

/// <summary>Cuts out of the text the span an occurrence points at, so that no other code turns its offsets into indexes.</summary>
internal static class TextSlicer
{
    /// <summary>The text of the half-open span the occurrence points at, exactly the code points the service analysed.</summary>
    /// <param name="text">The text that was sent to the service.</param>
    /// <param name="occurrence">An occurrence the service found in that text.</param>
    /// <returns>The code points from the start of the occurrence up to, and not including, its end.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The occurrence ends before it starts or falls outside the text.</exception>
    internal static string Slice(string text, Occurrence occurrence)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        if (occurrence.End < occurrence.Start)
        {
            throw new ArgumentOutOfRangeException(nameof(occurrence), "The occurrence ends before it starts.");
        }

        return Cut(text, occurrence.Start, occurrence.End);
    }

    /// <summary>The text of the half-open span between two positions in code points.</summary>
    /// <param name="text">The text to cut.</param>
    /// <param name="start">Where the span starts, in code points.</param>
    /// <param name="end">Where the span ends, in code points, not included.</param>
    /// <returns>The code points from the start up to, and not including, the end.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The span ends before it starts or falls outside the text.</exception>
    internal static string Span(string text, int start, int end)
    {
        if (end < start)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "The span ends before it starts.");
        }

        return Cut(text, start, end);
    }

    private static string Cut(string text, int start, int end) => text[OffsetConverter.ToUtf16Index(text, start)..OffsetConverter.ToUtf16Index(text, end)];
}
