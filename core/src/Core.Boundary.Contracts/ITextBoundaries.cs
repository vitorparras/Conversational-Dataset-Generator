namespace Core.Boundary.Contracts;

/// <summary>
/// What the core asks about where a text can be cut and what an occurrence points at. The questions are answered in one place of the boundary, the one that knows what a code point is,
/// so that no other code of the core counts them.
/// </summary>
public interface ITextBoundaries
{
    /// <summary>Whether the index falls between the two halves of a surrogate pair, where no cut of the text can be.</summary>
    /// <param name="text">The text the index points into.</param>
    /// <param name="utf16Index">An index of the text as the core holds it, from zero to its length.</param>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the text.</exception>
    bool SplitsPair(string text, int utf16Index);

    /// <summary>The span of the text an occurrence points at, exactly the code points the service analysed.</summary>
    /// <param name="text">The text that was sent to the service.</param>
    /// <param name="occurrence">An occurrence the service found in that text, with its offsets in code points.</param>
    /// <exception cref="ArgumentOutOfRangeException">The occurrence ends before it starts or falls outside the text.</exception>
    string Slice(string text, Occurrence occurrence);
}
