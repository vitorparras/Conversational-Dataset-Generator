using Core.Boundary.Contracts;

namespace Core.Boundary.Text;

/// <summary>The answer to where a text can be cut and what an occurrence points at, given by the one converter that tells a surrogate pair from two units.</summary>
public sealed class TextBoundaries : ITextBoundaries
{
    private const int PairLength = 2;

    /// <inheritdoc />
    public bool SplitsPair(string text, int utf16Index) => OffsetConverter.FallsInsidePair(text, utf16Index);

    /// <inheritdoc />
    public string Slice(string text, Occurrence occurrence) => TextSlicer.Slice(text, occurrence);

    /// <inheritdoc />
    public string Span(string text, int start, int until) => TextSlicer.Span(text, start, until);

    /// <inheritdoc />
    public int CodePointCount(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return OffsetConverter.ToCodePointOffset(text, text.Length);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> CodePoints(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var points = new List<string>(text.Length);
        for (var at = 0; at < text.Length; at += points[^1].Length)
        {
            points.Add(text.Substring(at, at + 1 < text.Length && char.IsSurrogatePair(text[at], text[at + 1]) ? PairLength : 1));
        }

        return points;
    }
}
