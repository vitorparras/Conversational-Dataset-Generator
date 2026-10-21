namespace Core.Boundary.Text;

/// <summary>
/// The one place an offset changes unit: code points, as the language service counts them, and UTF-16 indexes, as
/// the core indexes its text. A surrogate pair is one code point; any other UTF-16 unit, a lone surrogate included,
/// is one code point too.
/// </summary>
public static class OffsetConverter
{
    private const string OutsideText = "The offset is outside the text.";
    private const string InsidePair = "The offset falls between the two halves of a surrogate pair.";
    private const int PairWidth = 2;

    /// <summary>The UTF-16 index where the given code point offset falls in the text.</summary>
    /// <param name="text">The text the offset was measured in.</param>
    /// <param name="codePointOffset">An offset in code points, from zero to the number of code points in the text.</param>
    /// <returns>The UTF-16 index of the same position.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The offset is negative or past the end of the text.</exception>
    public static int ToUtf16Index(string text, int codePointOffset)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (codePointOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(codePointOffset), OutsideText);
        }

        var index = 0;
        for (var counted = 0; counted < codePointOffset; counted++)
        {
            if (index >= text.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(codePointOffset), OutsideText);
            }

            index += WidthAt(text, index);
        }

        return index;
    }

    /// <summary>The code point offset of the given UTF-16 index in the text; an index between the halves of a pair is never rounded.</summary>
    /// <param name="text">The text the index points into.</param>
    /// <param name="utf16Index">A UTF-16 index, from zero to the length of the text.</param>
    /// <returns>The number of code points before the index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the text or falls inside a surrogate pair.</exception>
    public static int ToCodePointOffset(string text, int utf16Index)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (utf16Index < 0 || utf16Index > text.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(utf16Index), OutsideText);
        }

        if (utf16Index > 0 && char.IsSurrogatePair(text, utf16Index - 1))
        {
            throw new ArgumentOutOfRangeException(nameof(utf16Index), InsidePair);
        }

        var offset = 0;
        for (var index = 0; index < utf16Index; index += WidthAt(text, index))
        {
            offset++;
        }

        return offset;
    }

    private static int WidthAt(string text, int index) => char.IsSurrogatePair(text, index) ? PairWidth : 1;
}
