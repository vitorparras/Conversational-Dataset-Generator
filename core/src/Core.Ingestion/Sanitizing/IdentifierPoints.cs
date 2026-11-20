using System.Globalization;

namespace Core.Ingestion.Sanitizing;

/// <summary>
/// What each code point, as the text that writes it, is to the removal of identifiers: a digit of any script, a character nobody sees, a separator that may split a number, or a
/// break no number spans. The code points come from the text boundaries, which alone count them.
/// </summary>
internal static class IdentifierPoints
{
    /// <summary>The separators that stand in the place of a dot, a slash or a dash, besides the spaces and the dashes of every script.</summary>
    private const string Separators = "./\\:()_\u00B7\u2027\u2024\u2022\u2219\u22C5\u3002\uFF0E\uFE52\u2044\u2215\uFF0F\u2212";

    /// <summary>The ends of a line or a page.</summary>
    private const string Breaks = "\n\r\f\v\u0085\u2028\u2029";

    /// <summary>The minus sign, which stands in the place of a dash.</summary>
    private const string Minus = "\u2212";

    internal static bool IsDigit(string point) => char.IsDigit(point, 0);

    /// <summary>A character of format, such as the space of zero width or the soft hyphen, which no reading shows.</summary>
    internal static bool IsInvisible(string point) => CharUnicodeInfo.GetUnicodeCategory(point, 0) == UnicodeCategory.Format;

    internal static bool IsBreak(string point) => Breaks.Contains(point, StringComparison.Ordinal);

    /// <summary>A space on the same line.</summary>
    internal static bool IsSpace(string point) => char.IsWhiteSpace(point, 0) && !IsBreak(point);

    /// <summary>A dash of any script, or the minus sign that stands in its place.</summary>
    internal static bool IsDash(string point) => CharUnicodeInfo.GetUnicodeCategory(point, 0) == UnicodeCategory.DashPunctuation || point == Minus;

    /// <summary>A visible character that may stand between two digits of one number.</summary>
    internal static bool IsSeparator(string point) => IsSpace(point) || IsDash(point) || Separators.Contains(point, StringComparison.Ordinal);

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
}
