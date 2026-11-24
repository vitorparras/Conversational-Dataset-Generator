using System.Globalization;
using System.Security.Cryptography;

namespace Core.Ingestion.Contracts.Dating;

/// <summary>
/// The shift of the dates of a domain (decision 0020): one whole number of days, never zero, drawn once for the domain and never told, that the export adds to every moment it
/// takes out. Every moment moves by the same quantity, so each interval stays exact and only the anchoring in the outside world is lost.
/// </summary>
public sealed class DateShift
{
    private const int TenYears = 3650;

    private DateShift(int days)
    {
        Days = days;
    }

    /// <summary>The most days a shift moves a moment, either way: ten years.</summary>
    public static int MostDays => TenYears;

    /// <summary>How many days the moments move: never zero, and at most <see cref="MostDays"/> either way.</summary>
    public int Days { get; }

    /// <summary>The text a store seals, which <see cref="Read"/> takes back.</summary>
    public string Stored => Days.ToString(CultureInfo.InvariantCulture);

    /// <summary>A shift drawn by the cryptographic generator, every allowed number of days, either way, equally likely.</summary>
    public static DateShift Draw() => FromDrawn(RandomNumberGenerator.GetInt32(-MostDays, MostDays));

    /// <summary>The shift of a number drawn from minus <see cref="MostDays"/> to <see cref="MostDays"/> less one: zero and every number above it move up by one, so zero never comes.</summary>
    /// <param name="drawn">The number the generator gave.</param>
    internal static DateShift FromDrawn(int drawn) => new(drawn >= 0 ? drawn + 1 : drawn);

    /// <summary>The shift a stored text says, or none when the text is not a number of days a shift may have.</summary>
    /// <param name="stored">The text as <see cref="Stored"/> wrote it.</param>
    public static DateShift? Read(string? stored) =>
        int.TryParse(stored, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var days) && Allowed(days) ? new DateShift(days) : null;

    /// <summary>The moment moved by the shift, in the offset it has; two moments moved by the same shift keep the interval between them to the tick.</summary>
    /// <param name="moment">The moment as the domain keeps it.</param>
    public DateTimeOffset Apply(DateTimeOffset moment) => moment.AddTicks(Days * TimeSpan.TicksPerDay);

    /// <summary>Names the type and never the days, so that a shift written to a log or a page by mistake tells nothing.</summary>
    public override string ToString() => nameof(DateShift);

    private static bool Allowed(int days) => days != 0 && Math.Abs((long)days) <= MostDays;
}
