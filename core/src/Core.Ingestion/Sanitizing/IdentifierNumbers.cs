using System.Globalization;
using System.Text;

namespace Core.Ingestion.Sanitizing;

/// <summary>
/// Finds the identifiers made of digits by the sequence of their digits, whatever stands between them: invisible characters, as many as there are, and up to three separators in a
/// row, so that a number split by a space of zero width, an odd dot or irregular spacing is the same number. A run of eight digits or more is an identifier, and so is a run closed by
/// a check digit after a dash; a date, a time, and a plain number after a currency or before a unit stay, since the policy keeps them on purpose.
/// </summary>
internal static class IdentifierNumbers
{
    private const int IdentifierDigits = 8;
    private const int AccountDigits = 6;
    private const int MaximumSeparators = 3;
    private const int MaximumSpacesToContext = 2;

    /// <summary>The units a quantity is written with, each between bars, read in any case.</summary>
    private const string Units = "|kg|g|mg|t|ton|tonelada|toneladas|quilo|quilos|grama|gramas|l|ml|litro|litros|m|km|cm|mm|metro|metros|m²|m³|km²|ha|hectare|hectares|"
        + "real|reais|dólar|dólares|euro|euros|mil|milhão|milhões|bilhão|bilhões|un|unidade|unidades|";

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
            if (MarkerOf(text, run) is { } marker)
            {
                yield return new IdentifierSpan(Opened(text, run.Start), run.End, marker);
            }

            at = run.Next;
        }
    }

    private static Run RunFrom(string[] text, int start)
    {
        var (end, digits, separators, at) = (start + 1, 1, 0, start + 1);
        for (; at < text.Length && Continues(text[at], separators); at++)
        {
            if (IdentifierPoints.IsDigit(text[at]))
            {
                (end, digits, separators) = (at + 1, digits + 1, 0);
            }
            else
            {
                separators += IdentifierPoints.IsInvisible(text[at]) ? 0 : 1;
            }
        }

        return new Run(start, end, digits, at);
    }

    private static bool Continues(string point, int separators) =>
        IdentifierPoints.IsDigit(point) || IdentifierPoints.IsInvisible(point) || (separators < MaximumSeparators && IdentifierPoints.IsSeparator(point));

    private static SanitizationMarker? MarkerOf(string[] text, Run run)
    {
        var shape = string.Concat(text[run.Start..run.End].Select(ShapeOf));
        if (run.Digits < AccountDigits || IsEnclosed(text, run) || IsKept(text, run, shape))
        {
            return null;
        }

        if (run.Digits >= IdentifierDigits)
        {
            return Opened(text, run.Start) < run.Start || NumberShapes.IsPhone(shape, run.Digits) ? SanitizationMarker.Contact : SanitizationMarker.Document;
        }

        return NumberShapes.IsAccount(shape) ? SanitizationMarker.Document : null;
    }

    /// <summary>A run glued to a letter on both sides, across invisible characters, which is part of a code and not a closed form.</summary>
    private static bool IsEnclosed(string[] text, Run run)
    {
        var before = IdentifierPoints.Visible(text, run.Start - 1, -1);
        var after = IdentifierPoints.Visible(text, run.End, 1);
        return before >= 0 && after < text.Length && char.IsLetter(text[before], 0) && char.IsLetter(text[after], 0);
    }

    /// <summary>A date or a time, or a plain number after a currency or before a unit: what the policy keeps on purpose.</summary>
    private static bool IsKept(string[] text, Run run, string shape) =>
        NumberShapes.IsDateOrTime(shape) || (NumberShapes.IsPlainNumber(shape) && (CurrencyBefore(text, run.Start) || UnitAfter(text, run.End)));

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
        while (at < text.Length && (char.IsLetter(text[at], 0) || text[at] is "%" or "²" or "³"))
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

    /// <summary>A run of digits: where its first digit is, just past its last digit, how many digits it holds and where the search goes on.</summary>
    private readonly record struct Run(int Start, int End, int Digits, int Next);
}
