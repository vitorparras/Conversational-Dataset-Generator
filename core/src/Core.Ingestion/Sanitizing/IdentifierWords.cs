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

    /// <summary>The month abbreviations, each between bars.</summary>
    private const string Months = "|JAN|FEV|MAR|ABR|MAI|JUN|JUL|AGO|SET|OUT|NOV|DEZ|";

    /// <summary>The forms read code point by code point: the old plate, the plate of the common market, both with or without a separator, and the physical address of a device.</summary>
    private static readonly Func<string, bool>[][] Forms =
    [
        [Letter, Letter, Letter, Digit, LetterOrDigit, Digit, Digit],
        [Letter, Letter, Letter, PlateSeparator, Digit, LetterOrDigit, Digit, Digit],
        [Hex, Hex, DeviceSeparator, Hex, Hex, DeviceSeparator, Hex, Hex, DeviceSeparator, Hex, Hex, DeviceSeparator, Hex, Hex, DeviceSeparator, Hex, Hex],
    ];

    /// <summary>The plates, the device addresses and the electronic addresses in the text, each as a contact or a document.</summary>
    internal static IEnumerable<IdentifierSpan> Find(string[] text)
    {
        for (var at = 0; at < text.Length; at++)
        {
            if (IdentifierPoints.IsInvisible(text[at]))
            {
                continue; // a form starts at a visible code point, and starting from each invisible one would read a long run of them once per position
            }

            foreach (var end in Forms.Select(form => Matched(text, at, form)).Where(end => end > at && !IsMonthAndYear(text, at, end)))
            {
                yield return new IdentifierSpan(at, end, SanitizationMarker.Document);
            }

            if (text[at] == "@" && Address(text, at) is { } address)
            {
                yield return address;
            }
        }
    }

    private static bool Letter(string point) => IdentifierPoints.IsAsciiLetter(point);

    private static bool Digit(string point) => IdentifierPoints.IsDigit(point);

    private static bool LetterOrDigit(string point) => Letter(point) || Digit(point);

    private static bool Hex(string point) => IdentifierPoints.IsHex(point);

    private static bool PlateSeparator(string point) => point == "." || IdentifierPoints.IsDash(point);

    private static bool DeviceSeparator(string point) => point == ":" || IdentifierPoints.IsDash(point);

    /// <summary>Just past the last code point of the form, read from the position given across invisible characters and bounded on both sides; the position itself when it does not match.</summary>
    private static int Matched(string[] text, int start, Func<string, bool>[] form)
    {
        var at = start;
        foreach (var slot in form)
        {
            at = IdentifierPoints.Visible(text, at, 1);
            if (at >= text.Length || !slot(text[at++]))
            {
                return start;
            }
        }

        return Bounded(text, start - 1, -1) && Bounded(text, at, 1) ? at : start;
    }

    /// <summary>Whether the visible code point from the position given, in the direction given, is outside the text or neither a letter nor a digit.</summary>
    private static bool Bounded(string[] text, int at, int step)
    {
        at = IdentifierPoints.Visible(text, at, step);
        return at < 0 || at >= text.Length || !IdentifierPoints.IsWord(text[at]);
    }

    /// <summary>A month abbreviation and a year of this century or the last, the plate form a date takes.</summary>
    private static bool IsMonthAndYear(string[] text, int start, int end)
    {
        var visible = IdentifierPoints.VisibleText(text, start, end);
        return Months.Contains($"|{visible[..MonthLetters].ToUpperInvariant()}|", StringComparison.Ordinal) && visible[^YearDigits..] is ['1' or '2', '9' or '0', _, _];
    }

    /// <summary>
    /// The electronic address around the at sign: a local part of letters of any script, digits and the usual signs, and a domain of two labels or more whose last one is
    /// letters only.
    /// </summary>
    private static IdentifierSpan? Address(string[] text, int at)
    {
        var start = LocalStart(text, at);
        var end = DomainEnd(text, at);
        return start < at && IsDomainName(IdentifierPoints.VisibleText(text, at + 1, end)) ? new IdentifierSpan(start, end, SanitizationMarker.Contact) : null;
    }

    private static int LocalStart(string[] text, int at)
    {
        var start = at;
        for (var back = at - 1; back >= 0 && (IsLocal(text[back]) || IdentifierPoints.IsInvisible(text[back])); back--)
        {
            start = IsLocal(text[back]) ? back : start;
        }

        return start;
    }

    /// <summary>Just past the last letter or digit of the domain, its closing dots and dashes left out.</summary>
    private static int DomainEnd(string[] text, int at)
    {
        var end = at + 1;
        while (end < text.Length && (IsDomain(text[end]) || IdentifierPoints.IsInvisible(text[end])))
        {
            end++;
        }

        while (end > at + 1 && !IsAddressWord(text[end - 1]))
        {
            end--;
        }

        return end;
    }

    private static bool IsDomainName(string domain)
    {
        var labels = domain.Split('.');
        return labels.Length > 1 && Array.TrueForAll(labels, label => label.Length > 0) && labels[^1].Length >= MinimumTopLevel && labels[^1].All(char.IsLetter);
    }

    /// <summary>A visible letter of any script or a digit.</summary>
    private static bool IsAddressWord(string point) => !IdentifierPoints.IsInvisible(point) && IdentifierPoints.IsWord(point);

    private static bool IsLocal(string point) => IsAddressWord(point) || LocalSigns.Contains(point, StringComparison.Ordinal);

    private static bool IsDomain(string point) => IsAddressWord(point) || point is "." or "-";
}
