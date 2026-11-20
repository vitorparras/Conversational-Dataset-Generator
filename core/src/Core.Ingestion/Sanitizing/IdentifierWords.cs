using System.Globalization;
using System.Text;

namespace Core.Ingestion.Sanitizing;

/// <summary>
/// Finds the identifiers that hold letters, by their closed form, reading across invisible characters: the plate of a vehicle, old or of the common market, the physical address of a
/// device and the electronic address. A month abbreviation followed by a year, as "mar-2024", is a date and stays.
/// </summary>
internal static class IdentifierWords
{
    private const int MinimumTopLevel = 2;
    private const int MonthLetters = 3;
    private const int YearDigits = 4;

    /// <summary>The signs a local part holds besides letters and digits.</summary>
    private const string LocalSigns = "._%+-";

    private static readonly Func<Rune, bool> Letter = IdentifierRunes.IsAsciiLetter;
    private static readonly Func<Rune, bool> Digit = IdentifierRunes.IsDigit;
    private static readonly Func<Rune, bool> LetterOrDigit = rune => IdentifierRunes.IsAsciiLetter(rune) || IdentifierRunes.IsDigit(rune);
    private static readonly Func<Rune, bool> Hex = IdentifierRunes.IsHex;
    private static readonly Func<Rune, bool> PlateSeparator = rune => rune.Value == '.' || Rune.GetUnicodeCategory(rune) == UnicodeCategory.DashPunctuation;
    private static readonly Func<Rune, bool> DeviceSeparator = rune => rune.Value == ':' || Rune.GetUnicodeCategory(rune) == UnicodeCategory.DashPunctuation;

    private static readonly Func<Rune, bool>[][] Forms =
    [
        [Letter, Letter, Letter, Digit, LetterOrDigit, Digit, Digit],
        [Letter, Letter, Letter, PlateSeparator, Digit, LetterOrDigit, Digit, Digit],
        [Hex, Hex, DeviceSeparator, Hex, Hex, DeviceSeparator, Hex, Hex, DeviceSeparator, Hex, Hex, DeviceSeparator, Hex, Hex, DeviceSeparator, Hex, Hex],
    ];

    private static readonly HashSet<string> Months = new(["JAN", "FEV", "MAR", "ABR", "MAI", "JUN", "JUL", "AGO", "SET", "OUT", "NOV", "DEZ"], StringComparer.Ordinal);

    /// <summary>The plates, the device addresses and the electronic addresses in the text, each as a contact or a document.</summary>
    internal static IEnumerable<IdentifierSpan> Find(Rune[] text)
    {
        for (var at = 0; at < text.Length; at++)
        {
            foreach (var end in Forms.Select(form => Matched(text, at, form)).Where(end => end > at && !IsMonthAndYear(text, at, end)))
            {
                yield return new IdentifierSpan(at, end, SanitizationMarker.Document);
            }

            if (text[at].Value == '@' && Address(text, at) is { } address)
            {
                yield return address;
            }
        }
    }

    /// <summary>Just past the last code point of the form, read from the position given across invisible characters and bounded on both sides; the position itself when it does not match.</summary>
    private static int Matched(Rune[] text, int start, Func<Rune, bool>[] form)
    {
        var at = start;
        foreach (var slot in form)
        {
            at = IdentifierRunes.Visible(text, at, 1);
            if (at >= text.Length || !slot(text[at++]))
            {
                return start;
            }
        }

        return Bounded(text, start - 1, -1) && Bounded(text, at, 1) ? at : start;
    }

    /// <summary>Whether the visible code point from the position given, in the direction given, is outside the text or neither a letter nor a digit.</summary>
    private static bool Bounded(Rune[] text, int at, int step)
    {
        at = IdentifierRunes.Visible(text, at, step);
        return at < 0 || at >= text.Length || !IdentifierRunes.IsWord(text[at]);
    }

    /// <summary>A month abbreviation and a year of this century or the last, the plate form a date takes.</summary>
    private static bool IsMonthAndYear(Rune[] text, int start, int end)
    {
        var visible = IdentifierRunes.VisibleText(text, start, end);
        return Months.Contains(visible[..MonthLetters].ToUpperInvariant()) && visible[^YearDigits..] is ['1' or '2', '9' or '0', _, _];
    }

    /// <summary>The electronic address around the at sign: a local part of letters, digits and the usual signs, and a domain of two labels or more whose last one is letters only.</summary>
    private static IdentifierSpan? Address(Rune[] text, int at)
    {
        var start = LocalStart(text, at);
        var end = DomainEnd(text, at);
        return start < at && IsDomainName(IdentifierRunes.VisibleText(text, at + 1, end)) ? new IdentifierSpan(start, end, SanitizationMarker.Contact) : null;
    }

    private static int LocalStart(Rune[] text, int at)
    {
        var start = at;
        for (var back = at - 1; back >= 0 && (IsLocal(text[back]) || IdentifierRunes.IsInvisible(text[back])); back--)
        {
            start = IsLocal(text[back]) ? back : start;
        }

        return start;
    }

    /// <summary>Just past the last letter or digit of the domain, its closing dots and dashes left out.</summary>
    private static int DomainEnd(Rune[] text, int at)
    {
        var end = at + 1;
        while (end < text.Length && (IsDomain(text[end]) || IdentifierRunes.IsInvisible(text[end])))
        {
            end++;
        }

        while (end > at + 1 && !LetterOrDigit(text[end - 1]))
        {
            end--;
        }

        return end;
    }

    private static bool IsDomainName(string domain)
    {
        var labels = domain.Split('.');
        return labels.Length > 1 && Array.TrueForAll(labels, label => label.Length > 0) && labels[^1].Length >= MinimumTopLevel && labels[^1].All(char.IsAsciiLetter);
    }

    private static bool IsLocal(Rune rune) => LetterOrDigit(rune) || LocalSigns.Contains(rune.ToString(), StringComparison.Ordinal);

    private static bool IsDomain(Rune rune) => LetterOrDigit(rune) || rune.Value is '.' or '-';
}
