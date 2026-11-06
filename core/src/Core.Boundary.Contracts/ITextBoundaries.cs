namespace Core.Boundary.Contracts;

/// <summary>
/// What the core asks about where a text can be cut. The question is answered in one place of the boundary, the one that knows what a code point is,
/// so that no other code of the core counts them.
/// </summary>
public interface ITextBoundaries
{
    /// <summary>Whether the index falls between the two halves of a surrogate pair, where no cut of the text can be.</summary>
    /// <param name="text">The text the index points into.</param>
    /// <param name="utf16Index">An index of the text as the core holds it, from zero to its length.</param>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the text.</exception>
    bool SplitsPair(string text, int utf16Index);
}
