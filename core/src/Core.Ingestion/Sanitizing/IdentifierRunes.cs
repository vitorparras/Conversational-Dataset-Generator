using System.Globalization;
using System.Text;

namespace Core.Ingestion.Sanitizing;

/// <summary>What each code point is to the removal of identifiers: a digit of any script, a character nobody sees, a separator that may split a number, or a break no number spans.</summary>
internal static class IdentifierRunes
{
    /// <summary>The separators that stand in the place of a dot, a slash or a dash, besides the spaces and the dashes of every script.</summary>
    private const string Separators = "./\\:()_\u00B7\u2027\u2024\u2022\u2219\u22C5\u3002\uFF0E\uFE52\u2044\u2215\uFF0F\u2212";

    /// <summary>The ends of a line or a page.</summary>
    private const string Breaks = "\n\r\f\v\u0085\u2028\u2029";

    internal static bool IsDigit(Rune rune) => Rune.IsDigit(rune);

    /// <summary>A character of format, such as the space of zero width or the soft hyphen, which no reading shows.</summary>
    internal static bool IsInvisible(Rune rune) => Rune.GetUnicodeCategory(rune) == UnicodeCategory.Format;

    internal static bool IsBreak(Rune rune) => Breaks.Contains(rune.ToString(), StringComparison.Ordinal);

    /// <summary>A space on the same line.</summary>
    internal static bool IsSpace(Rune rune) => Rune.IsWhiteSpace(rune) && !IsBreak(rune);

    /// <summary>A dash of any script, or the minus sign that stands in its place.</summary>
    internal static bool IsDash(Rune rune) => Rune.GetUnicodeCategory(rune) == UnicodeCategory.DashPunctuation || rune.Value == '\u2212';

    /// <summary>A visible character that may stand between two digits of one number.</summary>
    internal static bool IsSeparator(Rune rune) => IsSpace(rune) || IsDash(rune) || Separators.Contains(rune.ToString(), StringComparison.Ordinal);

    /// <summary>A letter or a digit, which glues to an identifier and so bounds none.</summary>
    internal static bool IsWord(Rune rune) => Rune.IsLetterOrDigit(rune);

    internal static bool IsAsciiLetter(Rune rune) => rune.IsAscii && char.IsAsciiLetter((char)rune.Value);

    internal static bool IsHex(Rune rune) => rune.IsAscii && char.IsAsciiHexDigit((char)rune.Value);

    /// <summary>The code points of the text, a broken surrogate read as the replacement character.</summary>
    internal static Rune[] Of(IEnumerable<string> points) => [.. points.Select(point => Rune.TryGetRuneAt(point, 0, out var rune) ? rune : Rune.ReplacementChar)];

    /// <summary>The first position from the one given, forward or back, whose code point is not invisible; outside the text when there is none.</summary>
    internal static int Visible(Rune[] text, int at, int step)
    {
        while (at >= 0 && at < text.Length && IsInvisible(text[at]))
        {
            at += step;
        }

        return at;
    }

    /// <summary>The visible code points between the two positions, as a string.</summary>
    internal static string VisibleText(Rune[] text, int start, int end) => string.Concat(text[start..end].Where(rune => !IsInvisible(rune)).Select(rune => rune.ToString()));
}
