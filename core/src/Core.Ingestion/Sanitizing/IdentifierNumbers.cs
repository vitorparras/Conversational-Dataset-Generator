using System.Globalization;
using System.Text;

namespace Core.Ingestion.Sanitizing;

/// <summary>
/// Finds the identifiers made of digits by the sequence of their digits, whatever stands between them on the line. An invisible character is nothing; a gap of at most one mark
/// a number is punctuated with and at most three spaces is the usual writing, which joins any groups, so that a number split by a space of zero width, an odd dot or irregular
/// spacing is the same number; any other gap of spaces and punctuation, a list's, as a semicolon, a comma or a full stop followed by a space, a quote, a bracket or a long
/// spacing, joins only the groups of a layout a list does not take, the CPF, the CNPJ, the IMEI and the telephone with its area code, and otherwise splits the run. A run of eight
/// digits or more is an identifier, and so is a run closed by a check digit after a dash; a date, a time, a range of them, and a plain number after a currency or before a unit
/// stay, since the policy keeps them on purpose.
/// </summary>
internal static class IdentifierNumbers
{
    private const int IdentifierDigits = 8;
    private const int AccountDigits = 6;
    private const int MaximumSpacesToContext = 2;
    private const int MaximumSpacesInANumber = 3;

    /// <summary>The units a quantity is written with, each between bars, read in any case.</summary>
    private const string Units = "|kg|g|mg|t|ton|tonelada|toneladas|quilo|quilos|grama|gramas|l|ml|litro|litros|m|km|cm|mm|metro|metros|ha|hectare|hectares|"
        + "real|reais|dólar|dólares|euro|euros|mil|milhão|milhões|bilhão|bilhões|un|unidade|unidades|";

    /// <summary>
    /// The layouts, by the widths of their groups, a list does not take: the CPF, the CNPJ, the IMEI and the telephone with its area code, with or without the country code. The
    /// card number, whose four groups of four a list of years also takes, and the telephone with the country code and four digits before the dash, whose groups a table of
    /// numbers takes, are not among them.
    /// </summary>
    private static readonly Layout[] Layouts =
    [
        new([3, 3, 3, 2], SanitizationMarker.Document),
        new([2, 3, 3, 4, 2], SanitizationMarker.Document),
        new([2, 6, 6, 1], SanitizationMarker.Document),
        new([2, 4, 4], SanitizationMarker.Contact),
        new([2, 5, 4], SanitizationMarker.Contact),
        new([2, 2, 5, 4], SanitizationMarker.Contact),
    ];

    /// <summary>The identifiers of digits in the text, each with the separators between its digits and the plus sign or parenthesis that opens it.</summary>
    internal static IEnumerable<IdentifierSpan> Find(string[] text)
    {
        var at = 0;
        while (at < text.Length)
        {
            if (!IdentifierPoints.IsDigit(text[at]))
            {
                at++;
                continue;
            }

            var run = RunFrom(text, at);
            foreach (var span in SpansOf(text, run))
            {
                yield return span;
            }

            at = run.Next;
        }
    }

    /// <summary>The run whole when a list's gap splits it into the groups of a layout; otherwise each of its parts, as the usual writing joins them.</summary>
    private static IEnumerable<IdentifierSpan> SpansOf(string[] text, Run run)
    {
        var whole = new Part(run.Parts[0].Start, run.Parts[^1].End, run.Parts.Sum(part => part.Digits));
        if (run.Parts.Count > 1 && !IsEnclosed(text, whole) && Array.Find(Layouts, layout => layout.Fits(whole.Digits, run.Cuts)) is { } layout)
        {
            return [new IdentifierSpan(Opened(text, whole.Start), whole.End, layout.Marker)];
        }

        return run.Parts.Select(part => MarkerOf(text, part) is { } marker ? new IdentifierSpan(Opened(text, part.Start), part.End, marker) : null).OfType<IdentifierSpan>();
    }

    /// <summary>The run of digits from the one given, across invisible characters and gaps, up to the first break: its parts, split where a gap is a list's, and its cuts.</summary>
    private static Run RunFrom(string[] text, int start)
    {
        var (parts, cuts) = (new List<Part>(), new List<int>());
        var (part, digits, at) = (new Part(start, start + 1, 1), 1, start + 1);
        while (true)
        {
            if (at < text.Length && IdentifierPoints.IsDigit(text[at]))
            {
                (part, digits, at) = (part with { End = at + 1, Digits = part.Digits + 1 }, digits + 1, at + 1);
                continue;
            }

            var visible = IdentifierPoints.Visible(text, at, 1);
            if (visible < text.Length && IdentifierPoints.IsDigit(text[visible]))
            {
                at = visible;
                continue;
            }

            var gap = GapFrom(text, at);
            if (gap is Gap.Break)
            {
                parts.Add(part);
                return new Run(parts, cuts, gap.Next);
            }

            cuts.Add(digits);
            if (gap is Gap.List)
            {
                parts.Add(part);
                part = new Part(gap.Next, gap.Next, 0);
            }

            at = gap.Next;
        }
    }

    /// <summary>
    /// What stands from the position given up to the next digit: the usual writing, or a list's, of spaces and punctuation; or a break, when anything else, or the end, comes first.
    /// </summary>
    private static Gap GapFrom(string[] text, int at)
    {
        var start = at;
        while (at < text.Length && !IdentifierPoints.IsDigit(text[at]) && (IdentifierPoints.IsInvisible(text[at]) || IdentifierPoints.IsGap(text[at])))
        {
            at++;
        }

        if (at >= text.Length || !IdentifierPoints.IsDigit(text[at]))
        {
            return new Gap.Break(at);
        }

        return IsUsual([.. text[start..at].Where(point => !IdentifierPoints.IsInvisible(point))]) ? new Gap.Usual(at) : new Gap.List(at);
    }

    /// <summary>
    /// The usual writing of a number: at most one mark a number is punctuated with and at most three spaces, save a full stop, a comma or another end of an item followed by a
    /// space, which closes an item of a list or a sentence.
    /// </summary>
    private static bool IsUsual(List<string> gap)
    {
        var marks = gap.FindAll(point => !IdentifierPoints.IsSpace(point));
        var closes = gap.Skip(1).Where((point, at) => IdentifierPoints.IsSpace(point) && IdentifierPoints.IsItemEnd(gap[at])).Any();
        return gap.Count - marks.Count <= MaximumSpacesInANumber && marks.Count <= 1 && marks.TrueForAll(IdentifierPoints.IsNumberMark) && !closes;
    }

    private static SanitizationMarker? MarkerOf(string[] text, Part part)
    {
        var shape = string.Concat(text[part.Start..part.End].Select(ShapeOf));
        if (part.Digits < AccountDigits || IsEnclosed(text, part) || IsKept(text, part, shape))
        {
            return null;
        }

        if (part.Digits >= IdentifierDigits)
        {
            return Opened(text, part.Start) < part.Start || NumberShapes.IsPhone(shape, part.Digits) ? SanitizationMarker.Contact : SanitizationMarker.Document;
        }

        return NumberShapes.IsAccount(shape) ? SanitizationMarker.Document : null;
    }

    /// <summary>A part glued to a letter on both sides, across invisible characters, which is part of a code and not a closed form.</summary>
    private static bool IsEnclosed(string[] text, Part part)
    {
        var before = IdentifierPoints.Visible(text, part.Start - 1, -1);
        var after = IdentifierPoints.Visible(text, part.End, 1);
        return before >= 0 && after < text.Length && char.IsLetter(text[before], 0) && char.IsLetter(text[after], 0);
    }

    /// <summary>A date or a time, or a plain number after a currency or before a unit: what the policy keeps on purpose.</summary>
    private static bool IsKept(string[] text, Part part, string shape) =>
        NumberShapes.IsDateOrTime(shape) || (NumberShapes.IsPlainNumber(shape) && (CurrencyBefore(text, part.Start) || UnitAfter(text, part.End)));

    /// <summary>The code point as the shapes read it: nothing when invisible, the digit in ASCII, one dash for any dash, one space for any space, and itself otherwise.</summary>
    private static string ShapeOf(string point) => point switch
    {
        _ when IdentifierPoints.IsInvisible(point) => string.Empty,
        _ when IdentifierPoints.IsDigit(point) => ((int)CharUnicodeInfo.GetNumericValue(point, 0)).ToString(CultureInfo.InvariantCulture),
        _ when IdentifierPoints.IsDash(point) => "-",
        _ when char.IsWhiteSpace(point, 0) => " ",
        _ => point,
    };

    /// <summary>Where the identifier starts: one back when a plus sign or an opening parenthesis comes right before its first digit.</summary>
    private static int Opened(string[] text, int start) => start > 0 && text[start - 1] is "+" or "(" ? start - 1 : start;

    private static bool CurrencyBefore(string[] text, int start)
    {
        var at = Beside(text, start - 1, -1);
        return at >= 0 && CharUnicodeInfo.GetUnicodeCategory(text[at], 0) == UnicodeCategory.CurrencySymbol;
    }

    private static bool UnitAfter(string[] text, int end)
    {
        var at = Beside(text, end, 1);
        var word = new StringBuilder();
        while (at < text.Length && (char.IsLetter(text[at], 0) || text[at] == "%"))
        {
            _ = word.Append(text[at]);
            at++;
        }

        var unit = word.ToString();
        return unit.StartsWith('%') || Units.Contains($"|{unit}|", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The first position, from the one given and in the direction given, past invisible characters and at most two spaces on the same line.</summary>
    private static int Beside(string[] text, int at, int step)
    {
        var spaces = 0;
        while (at >= 0 && at < text.Length && Passes(text[at], spaces))
        {
            spaces += IdentifierPoints.IsSpace(text[at]) ? 1 : 0;
            at += step;
        }

        return at;
    }

    private static bool Passes(string point, int spaces) => IdentifierPoints.IsInvisible(point) || (IdentifierPoints.IsSpace(point) && spaces < MaximumSpacesToContext);

    /// <summary>A part of a run, joined by the usual writing: where its first digit is, just past its last digit, and how many digits it holds.</summary>
    private readonly record struct Part(int Start, int End, int Digits);

    /// <summary>A run of digits: its parts, the count of digits before each of its gaps, and where the search goes on.</summary>
    private sealed record Run(List<Part> Parts, List<int> Cuts, int Next);

    /// <summary>What stands between two digits, and where the next digit, or what breaks the run, is.</summary>
    private abstract record Gap(int Next)
    {
        internal sealed record Usual(int Next) : Gap(Next);

        internal sealed record List(int Next) : Gap(Next);

        internal sealed record Break(int Next) : Gap(Next);
    }

    /// <summary>A layout by the widths of its groups, and the marker of what it is.</summary>
    private sealed record Layout(int[] Widths, SanitizationMarker Marker)
    {
        /// <summary>The counts of digits between two groups, where a gap may fall.</summary>
        private int[] Bounds { get; } = [.. Widths[..^1].Select((_, at) => Widths[..(at + 1)].Sum())];

        /// <summary>As many digits as the layout holds, and every gap between two of its groups.</summary>
        internal bool Fits(int digits, List<int> cuts) => digits == Widths.Sum() && cuts.TrueForAll(cut => Bounds.Contains(cut));
    }
}
