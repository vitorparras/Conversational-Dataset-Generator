namespace Core.Ingestion.Contracts.Dating;

/// <summary>
/// How the shift of a domain is established (decision 0020), from the row of that domain alone: no row draws one; a row that opens to a shift gives it; a row that does not open,
/// or opens to anything else, is lost for good. Nothing is drawn over a row that exists, since a shift guessed or drawn again would put what was already exported under another
/// regime without anyone seeing it.
/// </summary>
public static class DateShiftRule
{
    /// <summary>The shift the row gives, a new one when there is none, or the loss.</summary>
    /// <param name="stored">The row of the domain, as the store found it.</param>
    public static DateShiftReading Establish(StoredDateShift stored)
    {
        ArgumentNullException.ThrowIfNull(stored);
        if (!stored.Held)
        {
            return new DateShiftReading(DateShiftOutcome.Drawn, DateShift.Draw());
        }

        return DateShift.Read(stored.Text) is { } kept ? new DateShiftReading(DateShiftOutcome.Kept, kept) : new DateShiftReading(DateShiftOutcome.Lost, null);
    }
}
