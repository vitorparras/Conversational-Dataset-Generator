namespace Core.Ingestion.Contracts.Dating;

/// <summary>What establishing the shift of a domain answers: the shift, or none when it is lost.</summary>
/// <param name="Outcome">Whether the shift was drawn now, was kept or is lost.</param>
/// <param name="Shift">The shift, none when it is lost; never a value guessed.</param>
public sealed record DateShiftReading(DateShiftOutcome Outcome, DateShift? Shift);
