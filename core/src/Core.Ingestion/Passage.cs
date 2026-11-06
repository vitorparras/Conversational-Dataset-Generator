namespace Core.Ingestion;

/// <summary>
/// A passage of the text of a unit: a half-open span of the indexes of the text as the reader holds it. It is not an offset of the language service (decision 0011): counting code points is the
/// work of the converter of the boundary alone, and that is where a limit that falls inside a pair of units is refused.
/// </summary>
/// <param name="Start">The index where the passage starts.</param>
/// <param name="End">The index just past the passage.</param>
public sealed record Passage(int Start, int End);
