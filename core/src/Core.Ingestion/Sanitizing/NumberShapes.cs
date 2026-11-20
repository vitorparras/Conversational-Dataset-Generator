using System.Globalization;

namespace Core.Ingestion.Sanitizing;

/// <summary>
/// The shapes a run of digits takes once its invisible characters are gone, its digits written in ASCII, its dashes as one dash and its spaces as one space: a date, a time, a plain
/// or grouped number, the check digit of a bank account and the last four digits of a telephone.
/// </summary>
internal static class NumberShapes
{
    private const int GroupWidth = 3;
    private const int PhoneTail = 4;
    private const int MinimumPhoneDigits = 8;
    private const int MaximumPhoneDigits = 11;
    private const int FieldWidth = 2;
    private const int YearWidth = 4;
    private const int LastDay = 31;
    private const int LastMonth = 12;
    private const int HoursInADay = 24;
    private const int MinutesInAnHour = 60;

    /// <summary>Every part between spaces that holds a digit is a date or a time, as a date followed by its hour, or a list of dates.</summary>
    internal static bool IsDateOrTime(string shape)
    {
        var parts = shape.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(part => part.Any(char.IsAsciiDigit)).ToList();
        return parts.Count > 0 && parts.TrueForAll(part => IsDate(part) || IsTime(part));
    }

    /// <summary>Digits alone, or one to three digits followed by groups of three, all split by the same dot, comma or space; with or without decimals after a last comma.</summary>
    internal static bool IsPlainNumber(string shape)
    {
        var whole = WithoutDecimals(shape);
        var separator = SeparatorOf(whole);
        if (separator == default)
        {
            return true;
        }

        var groups = whole.Split(separator);
        return separator is '.' or ',' or ' ' && groups[0].Length is >= 1 and <= GroupWidth && Array.TrueForAll(groups[1..], group => group.Length == GroupWidth) && Array.TrueForAll(groups, IsDigits);
    }

    /// <summary>A number closed by a dash and one check digit.</summary>
    internal static bool IsAccount(string shape) => shape.Length > FieldWidth && shape[^FieldWidth] == '-' && char.IsAsciiDigit(shape[^1]) && char.IsAsciiDigit(shape[^(FieldWidth + 1)]);

    /// <summary>Eight to eleven digits, no dot, whose last separator sets apart the last four digits.</summary>
    internal static bool IsPhone(string shape, int digits)
    {
        var tail = shape.Length - 1 - shape.LastIndexOf(shape.LastOrDefault(character => !char.IsAsciiDigit(character)));
        return digits is >= MinimumPhoneDigits and <= MaximumPhoneDigits && !shape.Contains('.', StringComparison.Ordinal) && tail == PhoneTail;
    }

    /// <summary>The number without the one or two decimals a last comma sets apart.</summary>
    private static string WithoutDecimals(string shape)
    {
        var comma = shape.LastIndexOf(',');
        return comma > 0 && shape.Length - comma - 1 <= FieldWidth && IsDigits(shape[(comma + 1)..]) ? shape[..comma] : shape;
    }

    private static bool IsTime(string part)
    {
        var fields = part.Split(':');
        return fields.Length is FieldWidth or FieldWidth + 1 && Array.TrueForAll(fields, IsDigits) && IsHour(fields[0]) && Array.TrueForAll(fields[1..], IsMinute);
    }

    private static bool IsHour(string field) => field.Length <= FieldWidth && Value(field) < HoursInADay;

    private static bool IsMinute(string field) => field.Length == FieldWidth && Value(field) < MinutesInAnHour;

    private static bool IsDate(string part)
    {
        var separator = SeparatorOf(part);
        var fields = part.Split(separator);
        return separator is '/' or '.' or '-' && Array.TrueForAll(fields, IsDigits) && fields switch
        {
            [var day, var month] => IsDayAndMonth(day, month) || IsMonthAndYear(day, month),
            [var day, var month, var year] => IsDayMonthAndYear(day, month, year) || IsDayMonthAndYear(year, month, day),
            _ => false,
        };
    }

    /// <summary>A day and a month, then a year of two or four digits; given the last field first, it reads the year first, as ISO writes it.</summary>
    private static bool IsDayMonthAndYear(string first, string month, string last) => IsDayAndMonth(first, month) && last.Length is FieldWidth or YearWidth;

    private static bool IsDayAndMonth(string day, string month) => day.Length <= FieldWidth && Value(day) is >= 1 and <= LastDay && IsMonth(month);

    private static bool IsMonthAndYear(string month, string year) => IsMonth(month) && year.Length == YearWidth;

    private static bool IsMonth(string month) => month.Length <= FieldWidth && Value(month) is >= 1 and <= LastMonth;

    private static bool IsDigits(string field) => field.Length > 0 && field.All(char.IsAsciiDigit);

    private static char SeparatorOf(string shape) => shape.FirstOrDefault(character => !char.IsAsciiDigit(character));

    private static int Value(string digits) => int.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);
}
