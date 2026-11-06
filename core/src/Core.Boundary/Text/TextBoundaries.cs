using Core.Boundary.Contracts;

namespace Core.Boundary.Text;

/// <summary>The answer to where a text can be cut, given by the one converter that tells a surrogate pair from two units.</summary>
public sealed class TextBoundaries : ITextBoundaries
{
    /// <inheritdoc />
    public bool SplitsPair(string text, int utf16Index) => OffsetConverter.FallsInsidePair(text, utf16Index);
}
