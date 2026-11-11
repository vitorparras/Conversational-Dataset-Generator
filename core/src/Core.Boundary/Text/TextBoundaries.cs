using Core.Boundary.Contracts;

namespace Core.Boundary.Text;

/// <summary>The answer to where a text can be cut and what an occurrence points at, given by the one converter that tells a surrogate pair from two units.</summary>
public sealed class TextBoundaries : ITextBoundaries
{
    /// <inheritdoc />
    public bool SplitsPair(string text, int utf16Index) => OffsetConverter.FallsInsidePair(text, utf16Index);

    /// <inheritdoc />
    public string Slice(string text, Occurrence occurrence) => TextSlicer.Slice(text, occurrence);

    /// <inheritdoc />
    public int CodePointCount(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return OffsetConverter.ToCodePointOffset(text, text.Length);
    }
}
