namespace Core.Persistence.Ingestion;

/// <summary>One number of the detail of a batch: a code (a form, a destination, or a form and its losses joined by a colon) and how many there are.</summary>
/// <param name="Code">The code, never a word of the material.</param>
/// <param name="Value">How many.</param>
public sealed record BatchCount(string Code, long Value);
