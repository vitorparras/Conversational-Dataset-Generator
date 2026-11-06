namespace Core.Ingestion;

/// <summary>Where a refusal points: the position of the unit among the units read, and the field of it that is missing or not allowed; never what the unit says.</summary>
/// <param name="Unit">The position of the unit among the units read, from zero; null when the finding is about the material as a whole.</param>
/// <param name="Field">The field of the unit; null when the finding is about the unit or the material as a whole.</param>
public sealed record Finding(int? Unit, MaterialField? Field);
