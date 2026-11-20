namespace Core.Ingestion.Sanitizing;

/// <summary>
/// Reads a run of digits on the line, from its first digit to what breaks it: an invisible character is nothing; a gap of at most one mark a number is punctuated with and at
/// most three spaces is the usual writing, which joins the digits into one part; any other gap of spaces and punctuation, a list's, starts a new part; anything else breaks the
/// run. It keeps the count of digits before every gap, so that the parts can be matched against the groups of a layout.
/// </summary>
internal static class NumberRuns
{
    private const int MaximumSpacesInANumber = 3;

    /// <summary>The run of digits from the one given, across invisible characters and gaps, up to the first break: its parts, split where a gap is a list's, and its cuts.</summary>
    internal static Run From(string[] text, int start)
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

            if (gap is Gap.Listed listed)
            {
                parts.Add(part);
                part = new Part(gap.Next, gap.Next, 0, digits, cuts.Count, listed.ClosesAnItem);
            }

            (part, digits, last) = (part with { End = gap.Next + 1, Digits = part.Digits + 1 }, digits + 1, gap.Next);
        }
    }

    /// <summary>What stands between the digit given and the next one: nothing seen, or a gap.</summary>
    private static Gap GapAfter(string[] text, int last)
    {
        var next = IdentifierPoints.Visible(text, last + 1, 1);
        if (next < text.Length && IdentifierPoints.IsDigit(text[next]))
        {
            return new Gap.None(next);
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
        return IsUsual(gap) && !closes ? new Gap.Usual(at) : new Gap.Listed(at, closes);
    }

    /// <summary>The usual writing of a number: at most one mark a number is punctuated with and at most three spaces.</summary>
    private static bool IsUsual(List<string> gap)
    {
        var marks = gap.FindAll(point => !IdentifierPoints.IsSpace(point));
        return gap.Count - marks.Count <= MaximumSpacesInANumber && marks.Count <= 1 && marks.TrueForAll(IdentifierPoints.IsNumberMark);
    }

    /// <summary>
    /// A part of a run, joined by the usual writing: where its first digit is, just past its last digit, how many digits it holds, how many digits of the run come before it,
    /// where its cuts start among the run's, and whether the list's gap before it closes an item.
    /// </summary>
    internal readonly record struct Part(int Start, int End, int Digits, int First, int CutFrom, bool AfterAnItem);

    /// <summary>A run of digits: its parts, the count of digits before each of its gaps, and where the search goes on.</summary>
    internal sealed record Run(List<Part> Parts, List<int> Cuts, int Next);

    /// <summary>What stands between two digits, and where the next digit, or what breaks the run, is.</summary>
    private abstract record Gap(int Next)
    {
        internal sealed record None(int Next) : Gap(Next);

        internal sealed record Usual(int Next) : Gap(Next);

        internal sealed record Listed(int Next, bool ClosesAnItem) : Gap(Next);

        internal sealed record Break(int Next) : Gap(Next);
    }
}
