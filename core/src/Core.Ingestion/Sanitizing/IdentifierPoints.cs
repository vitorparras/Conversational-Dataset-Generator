using System.Globalization;

namespace Core.Ingestion.Sanitizing;

/// <summary>
/// What each code point, as the text that writes it, is to the removal of identifiers: a digit of any script, a character nobody sees, a mark in the place of the dot, what may
/// stand between two digits of one number, or a break no number spans. Partir não é esconder (requirement 004): a space or a punctuation mark may split a number, save the signs
/// of a value; an emoji or a sign of arithmetic is signal and splits none (requirement 005). The code points come from the text boundaries, which alone count them.
/// </summary>
internal static class IdentifierPoints
{
    /// <summary>The fillers of Hangul, letters that show nothing.</summary>
    private const string Fillers = "\u115F\u1160\u3164\uFFA0";

    /// <summary>
    /// The marks a number is punctuated with, or that stand in their place: the dot, the comma, the slash, the colon and the parentheses, of any width, the dots of the middle,
    /// of a leader, of hyphenation and of an operator, the bullets, the slashes of division and of fraction, and the blank of braille, which shows as a space.
    /// </summary>
    private const string NumberMarks = ".,/:()\u00B7\u2022\u2024\u2027\u2219\u22C5\u2215\u2044\u2800\u3001\u3002\uFF08\uFF09\uFF0C\uFF0E\uFF0F\uFF1A";

    /// <summary>The commas, semicolons, full stops, ellipses and the marks of exclamation and question that, followed by a space, close an item of a list or a sentence.</summary>
    private const string ItemEnds = ",;.!?\u2026\uFF0C\uFF1B\u3001\uFF01\uFF1F\u3002\uFF0E";

    /// <summary>The ends of a line or a page.</summary>
    private const string Breaks = "\n\r\f\v\u0085\u2028\u2029";

    /// <summary>The minus sign, which stands in the place of a dash.</summary>
    private const string Minus = "\u2212";

    /// <summary>The value of the largest digit.</summary>
    private const int LargestDigit = 9;

    /// <summary>A digit of any script: of category Nd, or of category No with the value of one digit, as the circled and the superscript ones.</summary>
    internal static bool IsDigit(string point) =>
        char.IsDigit(point, 0) || (CharUnicodeInfo.GetUnicodeCategory(point, 0) == UnicodeCategory.OtherNumber && IsOneDigit(CharUnicodeInfo.GetNumericValue(point, 0)));

    /// <summary>Two digits both of category Nd or both of category No: a superscript after a digit of the line is its exponent, not one more digit of it.</summary>
    internal static bool AreOfOneKind(string first, string second) => char.IsDigit(first, 0) == char.IsDigit(second, 0);

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

    /// <summary>A visible character on the line that may stand between two digits of one number: a space, a punctuation mark or a mark in the place of the dot, not a sign of value.</summary>
    internal static bool IsGap(string point) => (IsSpace(point) || char.IsPunctuation(point, 0) || IsNumberMark(point)) && !IsSignOfValue(point);

    /// <summary>A mark a number is punctuated with, or that stands in its place: a dash of any script among them.</summary>
    internal static bool IsNumberMark(string point) => NumberMarks.Contains(point, StringComparison.Ordinal) || IsDash(point);

    /// <summary>A comma, a semicolon, a full stop, an ellipsis or a mark of exclamation or question, of any width.</summary>
    internal static bool IsItemEnd(string point) => ItemEnds.Contains(point, StringComparison.Ordinal);

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

    private static bool IsOneDigit(double value) => value is >= 0 and <= LargestDigit && double.IsInteger(value);

    /// <summary>A currency sign, the percent sign, the number sign or the asterisk, which make a number a value, an ordinal or a note, or join it in arithmetic, and so split none.</summary>
    private static bool IsSignOfValue(string point) => point is "%" or "#" or "*" || CharUnicodeInfo.GetUnicodeCategory(point, 0) == UnicodeCategory.CurrencySymbol;
}
