using System.Globalization;

namespace Core.Ingestion.Sanitizing;

/// <summary>
/// What each code point, as the text that writes it, is to the removal of identifiers: a digit of any script, a character nobody sees, a separator that may split a number, or a
/// break no number spans. Partir não é esconder (requirement 004): whatever on the line is neither a letter nor a digit may split a number, save the signs of a value. The code
/// points come from the text boundaries, which alone count them.
/// </summary>
internal static class IdentifierPoints
{
    /// <summary>The fillers of Hangul, letters that show nothing.</summary>
    private const string Fillers = "\u115F\u1160\u3164\uFFA0";

    /// <summary>The commas and semicolons that, followed by a space, close an item of a list.</summary>
    private const string ListMarks = ",;\uFF0C\uFF1B\u3001";

    /// <summary>The ends of a line or a page.</summary>
    private const string Breaks = "\n\r\f\v\u0085\u2028\u2029";

    /// <summary>The minus sign, which stands in the place of a dash.</summary>
    private const string Minus = "\u2212";

    internal static bool IsDigit(string point) => char.IsDigit(point, 0);

    /// <summary>
    /// A character no reading shows: of format, as the space of zero width or the soft hyphen; a mark of no width, as the variation selectors or the grapheme joiner; or a
    /// filler of Hangul.
    /// </summary>
    internal static bool IsInvisible(string point) =>
        CharUnicodeInfo.GetUnicodeCategory(point, 0) is UnicodeCategory.Format or UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark
        || Fillers.Contains(point, StringComparison.Ordinal);

    internal static bool IsBreak(string point) => Breaks.Contains(point, StringComparison.Ordinal);

    /// <summary>A space on the same line.</summary>
    internal static bool IsSpace(string point) => char.IsWhiteSpace(point, 0) && !IsBreak(point);

    /// <summary>A dash of any script, or the minus sign that stands in its place.</summary>
    internal static bool IsDash(string point) => CharUnicodeInfo.GetUnicodeCategory(point, 0) == UnicodeCategory.DashPunctuation || point == Minus;

    /// <summary>A visible character on the line that may stand between two digits of one number: neither a letter, a digit, a currency sign nor the percent sign.</summary>
    internal static bool IsSeparator(string point) => !IsBreak(point) && !IsInvisible(point) && !char.IsLetterOrDigit(point, 0) && !IsSignOfValue(point);

    /// <summary>A comma or a semicolon, of any width.</summary>
    internal static bool IsListMark(string point) => ListMarks.Contains(point, StringComparison.Ordinal);

    /// <summary>A letter or a digit, which glues to an identifier and so bounds none.</summary>
    internal static bool IsWord(string point) => char.IsLetterOrDigit(point, 0);

    internal static bool IsAsciiLetter(string point) => point.Length == 1 && char.IsAsciiLetter(point[0]);

    internal static bool IsHex(string point) => point.Length == 1 && char.IsAsciiHexDigit(point[0]);

    /// <summary>The code points of the text, as the boundaries split it.</summary>
    internal static string[] Of(IEnumerable<string> points) => [.. points];

    /// <summary>The first position from the one given, forward or back, whose code point is not invisible; outside the text when there is none.</summary>
    internal static int Visible(string[] text, int at, int step)
    {
        while (at >= 0 && at < text.Length && IsInvisible(text[at]))
        {
            at += step;
        }

        return at;
    }

    /// <summary>The visible code points between the two positions, as a string.</summary>
    internal static string VisibleText(string[] text, int start, int end) => string.Concat(text[start..end].Where(point => !IsInvisible(point)));

    /// <summary>A currency sign or the percent sign, which make a number a value and so split none.</summary>
    private static bool IsSignOfValue(string point) => point == "%" || CharUnicodeInfo.GetUnicodeCategory(point, 0) == UnicodeCategory.CurrencySymbol;
}
