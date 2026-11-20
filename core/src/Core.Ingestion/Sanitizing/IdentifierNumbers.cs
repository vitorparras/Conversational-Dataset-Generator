using System.Globalization;
using System.Text;

namespace Core.Ingestion.Sanitizing;

/// <summary>
/// Finds the identifiers made of digits by the sequence of their digits, whatever stands between them on the line. An invisible character is nothing; a gap of at most one mark
/// a number is punctuated with and at most three spaces is the usual writing, which joins any groups, so that a number split by a space of zero width, an odd dot or irregular
/// spacing is the same number. Any other gap of spaces and punctuation, a list's, as a semicolon, a comma or a full stop followed by a space, a quote, a bracket or a long
/// spacing, and two digits of different kinds side by side, as an exponent after its base, split the run into parts; consecutive parts join only into the groups of a layout a
/// list does not take, a document with its check digits or a telephone with its area code, and not after a currency or before a unit. A part of eight digits or more is an
/// identifier, and so is a part closed by a check digit after a dash; a date, a time, a range of them, and a plain number after a currency or before a unit stay, since the
/// policy keeps them on purpose.
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
    /// The layouts, by the widths of their groups, a list does not take: the CPF, the CNPJ, the RG, the PIS and the IMEI, which end in check digits and join also across the end
    /// of an item, and the telephone with its area code, with or without the country code, which a list of years separated by commas also takes, and so joins only across the
    /// other marks of a list. The card number, whose four groups of four a list of years takes, and the telephone with the country code and four digits before the dash, whose
    /// groups a table of numbers takes, are not among them.
    /// </summary>
    private static readonly Layout[] Layouts =
    [
        new([3, 3, 3, 2], SanitizationMarker.Document, AcrossItems: true),
        new([2, 3, 3, 4, 2], SanitizationMarker.Document, AcrossItems: true),
        new([2, 3, 3, 1], SanitizationMarker.Document, AcrossItems: true),
        new([3, 5, 2, 1], SanitizationMarker.Document, AcrossItems: true),
        new([2, 6, 6, 1], SanitizationMarker.Document, AcrossItems: true),
        new([2, 4, 4], SanitizationMarker.Contact, AcrossItems: false),
        new([2, 5, 4], SanitizationMarker.Contact, AcrossItems: false),
        new([2, 2, 5, 4], SanitizationMarker.Contact, AcrossItems: false),
    ];

    /// <summary>The most digits a layout holds.</summary>
    private static readonly int LongestLayout = Layouts.Max(layout => layout.Digits);

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

    /// <summary>From the first part on, the longest consecutive parts that fill a layout, whole; otherwise the part alone, as the usual writing joins it.</summary>
    private static IEnumerable<IdentifierSpan> SpansOf(string[] text, Run run)
    {
        var at = 0;
        while (at < run.Parts.Count)
        {
            if (LaidOut(text, run, at) is { } laid)
            {
                yield return laid.Span;
                at = laid.Next;
                continue;
            }

            if (MarkerOf(text, run.Parts[at]) is { } marker)
            {
                yield return new IdentifierSpan(Opened(text, run.Parts[at].Start), run.Parts[at].End, marker);
            }

            at++;
        }
    }

    /// <summary>The span of the longest two or more parts, from the one given, that write the groups of a layout, and the part past them; none when no such parts do.</summary>
    private static (IdentifierSpan Span, int Next)? LaidOut(string[] text, Run run, int first)
    {
        (IdentifierSpan Span, int Next)? laid = null;
        var (digits, crossesAnItem) = (run.Parts[first].Digits, false);
        for (var last = first + 1; last < run.Parts.Count && digits < LongestLayout; last++)
        {
            (digits, crossesAnItem) = (digits + run.Parts[last].Digits, crossesAnItem || run.Parts[last].AfterAnItem);
            var window = new Part(run.Parts[first].Start, run.Parts[last].End, digits, run.Parts[first].First, run.Parts[first].CutFrom, AfterAnItem: false);
            if (LayoutOf(text, window, CutsOf(run, first, last), crossesAnItem) is { } marker)
            {
                laid = (new IdentifierSpan(Opened(text, window.Start), window.End, marker), last + 1);
            }
        }

        return laid;
    }

    /// <summary>The marker of the layout the parts fill, when no letter encloses them, no currency comes before them and no unit after them.</summary>
    private static SanitizationMarker? LayoutOf(string[] text, Part window, List<int> cuts, bool crossesAnItem) =>
        IsEnclosed(text, window) || CurrencyBefore(text, window.Start) || UnitAfter(text, window.End) ? null : Array.Find(Layouts, layout => layout.Fits(window.Digits, cuts, crossesAnItem))?.Marker;

    /// <summary>The cuts between the first part given and the end of the last, counted from the first digit of the first.</summary>
    private static List<int> CutsOf(Run run, int first, int last)
    {
        var end = last + 1 < run.Parts.Count ? run.Parts[last + 1].CutFrom - 1 : run.Cuts.Count;
        return run.Cuts.GetRange(run.Parts[first].CutFrom, end - run.Parts[first].CutFrom).ConvertAll(cut => cut - run.Parts[first].First);
    }

    /// <summary>The run of digits from the one given, across invisible characters and gaps, up to the first break: its parts, split where a gap is a list's, and its cuts.</summary>
    private static Run RunFrom(string[] text, int start)
    {
        var (parts, cuts) = (new List<Part>(), new List<int>());
        var (part, digits, last) = (new Part(start, start + 1, 1, 0, 0, AfterAnItem: false), 1, start);
        while (true)
        {
            var gap = GapAfter(text, last);
            if (gap is Gap.Break)
            {
                parts.Add(part);
                return new Run(parts, cuts, gap.Next);
            }

            if (gap is not Gap.None)
            {
                cuts.Add(digits);
            }

            if (gap is Gap.List list)
            {
                parts.Add(part);
                part = new Part(gap.Next, gap.Next, 0, digits, cuts.Count, list.ClosesAnItem);
            }

            (part, digits, last) = (part with { End = gap.Next + 1, Digits = part.Digits + 1 }, digits + 1, gap.Next);
        }
    }

    /// <summary>What stands between the digit given and the next one: nothing seen, between two digits of one kind; a list's, between two of different kinds; or a gap.</summary>
    private static Gap GapAfter(string[] text, int last)
    {
        var next = IdentifierPoints.Visible(text, last + 1, 1);
        if (next < text.Length && IdentifierPoints.IsDigit(text[next]))
        {
            return IdentifierPoints.AreOfOneKind(text[last], text[next]) ? new Gap.None(next) : new Gap.List(next, ClosesAnItem: false);
        }

        return GapFrom(text, last + 1);
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

        var gap = text[start..at].Where(point => !IdentifierPoints.IsInvisible(point)).ToList();
        var closes = gap.Skip(1).Where((point, before) => IdentifierPoints.IsSpace(point) && IdentifierPoints.IsItemEnd(gap[before])).Any();
        return IsUsual(gap) && !closes ? new Gap.Usual(at) : new Gap.List(at, closes);
    }

    /// <summary>The usual writing of a number: at most one mark a number is punctuated with and at most three spaces.</summary>
    private static bool IsUsual(List<string> gap)
    {
        var marks = gap.FindAll(point => !IdentifierPoints.IsSpace(point));
        return gap.Count - marks.Count <= MaximumSpacesInANumber && marks.Count <= 1 && marks.TrueForAll(IdentifierPoints.IsNumberMark);
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

    /// <summary>
    /// A part of a run, joined by the usual writing: where its first digit is, just past its last digit, how many digits it holds, how many digits of the run come before it,
    /// where its cuts start among the run's, and whether the list's gap before it closes an item.
    /// </summary>
    private readonly record struct Part(int Start, int End, int Digits, int First, int CutFrom, bool AfterAnItem);

    /// <summary>A run of digits: its parts, the count of digits before each of its gaps, and where the search goes on.</summary>
    private sealed record Run(List<Part> Parts, List<int> Cuts, int Next);

    /// <summary>What stands between two digits, and where the next digit, or what breaks the run, is.</summary>
    private abstract record Gap(int Next)
    {
        internal sealed record None(int Next) : Gap(Next);

        internal sealed record Usual(int Next) : Gap(Next);

        internal sealed record List(int Next, bool ClosesAnItem) : Gap(Next);

        internal sealed record Break(int Next) : Gap(Next);
    }

    /// <summary>A layout by the widths of its groups, the marker of what it is, and whether it joins across the end of an item.</summary>
    private sealed record Layout(int[] Widths, SanitizationMarker Marker, bool AcrossItems)
    {
        internal int Digits { get; } = Widths.Sum();

        /// <summary>The counts of digits between two groups, where a gap may fall.</summary>
        private int[] Bounds { get; } = [.. Widths[..^1].Select((_, at) => Widths[..(at + 1)].Sum())];

        /// <summary>Its groups and no other, a gap between every two of them and none inside one, and no end of an item unless the layout joins across it.</summary>
        internal bool Fits(int digits, List<int> cuts, bool crossesAnItem) => digits == Digits && (AcrossItems || !crossesAnItem) && HasItsGroups(cuts);

        private bool HasItsGroups(List<int> cuts) => cuts.Count == Bounds.Length && cuts.TrueForAll(cut => Bounds.Contains(cut));
    }
}
