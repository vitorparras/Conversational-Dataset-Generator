using System.Globalization;

namespace Core.Persistence.Migrations;

/// <summary>The order of the steps is the order of their names: <c>NNNN-name</c> for the numbered ones, which come first, and <c>yyyyMMddHHmm-name</c> for the ones dated by their author.</summary>
internal static class MigrationNames
{
    internal const int NumberWidth = 4;
    internal const int MomentWidth = 12;
    private const string MomentFormat = "yyyyMMddHHmm";

    /// <summary>The names in the order the steps run; a name in neither format, or a dated one that is no moment of the calendar, is a defect of the build.</summary>
    internal static IReadOnlyList<string> Ordered(IEnumerable<string> names) =>
        [.. names.Select(Checked).Order(StringComparer.Ordinal)];

    private static string Checked(string name) =>
        StartsWithDigits(name, NumberWidth) || (StartsWithDigits(name, MomentWidth) && IsMoment(name[..MomentWidth]))
            ? name
            : throw new InvalidOperationException($"The migration '{name}' is named neither NNNN-name nor yyyyMMddHHmm-name with a moment of the calendar.");

    private static bool StartsWithDigits(string name, int width) =>
        name.Length > width && name[width] == '-' && name.Take(width).All(char.IsAsciiDigit);

    private static bool IsMoment(string digits) =>
        DateTime.TryParseExact(digits, MomentFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}
