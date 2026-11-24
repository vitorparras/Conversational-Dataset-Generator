using System.Globalization;
using System.Text.RegularExpressions;
using Core.Ingestion.Contracts.Dating;

namespace Core.Host.Tests.Dating;

/// <summary>
/// Where a text shows the shift of the dates. A shift is a few digits, which any page or log holds by chance (a year, a count, an identifier), so the bare number is not searched:
/// the forms are the ones a shift takes when written as what it is, a span of days, each not glued to a digit, a sign or a separator before it.
/// </summary>
internal static class ShiftSweep
{
    /// <summary>The forms the shift is searched in: the span as .NET writes it, the days in Portuguese and English, the ISO duration and the members of a type written out.</summary>
    public static IReadOnlyList<string> Forms(DateShift shift)
    {
        ArgumentNullException.ThrowIfNull(shift);
        var span = TimeSpan.FromDays(shift.Days);
        var days = shift.Stored;
        return
        [
            span.ToString("c", CultureInfo.InvariantCulture), span.ToString("g", CultureInfo.InvariantCulture), span.ToString("G", CultureInfo.InvariantCulture),
            $"{days} dia", $"{days} day", $"P{days}D", $"\"days\":{days}", $"Days = {days}", $"Days: {days}", $"shift={days}",
        ];
    }

    /// <summary>Each text that shows the shift in one of its forms, with the form found.</summary>
    public static IReadOnlyList<string> Showing(IEnumerable<string> texts, DateShift shift)
    {
        var forms = Forms(shift).Select(form => new Regex(@"(?<![\w.:+-])" + Regex.Escape(form), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5))).ToList();
        return [.. texts.SelectMany(text => forms.Where(form => form.IsMatch(text)).Select(form => $"{form}: {text}"))];
    }
}
