using Core.Boundary.Contracts;

namespace Core.Boundary.Text;

/// <summary>Cuts out of the text the span an occurrence points at, so that no other code turns its offsets into indexes.</summary>
public static class TextSlicer
{
    /// <summary>The text of the half-open span the occurrence points at, exactly the code points the service analysed.</summary>
    /// <param name="text">The text that was sent to the service.</param>
    /// <param name="occurrence">An occurrence the service found in that text.</param>
    /// <returns>The code points from the start of the occurrence up to, and not including, its end.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The occurrence ends before it starts or falls outside the text.</exception>
    public static string Slice(string text, Occurrence occurrence)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        if (occurrence.End < occurrence.Start)
        {
            throw new ArgumentOutOfRangeException(nameof(occurrence), "The occurrence ends before it starts.");
        }

        var start = OffsetConverter.ToUtf16Index(text, occurrence.Start);
        var end = OffsetConverter.ToUtf16Index(text, occurrence.End);
        return text[start..end];
    }
}
