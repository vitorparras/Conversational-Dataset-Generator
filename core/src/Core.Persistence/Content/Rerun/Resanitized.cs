namespace Core.Persistence.Content.Rerun;

/// <summary>What the rerun of an item produced: the text, its version and steps, the trail, the counts and untreated of each step, and whether the text is the one held.</summary>
public sealed record Resanitized
{
    /// <summary>The sanitized text; sealed on its way to the database.</summary>
    public required string Text { get; init; }

    /// <summary>The version of the list of steps that produced it.</summary>
    public required string Version { get; init; }

    /// <summary>The steps of the version, one name@version per line, written once per domain.</summary>
    public required string Steps { get; init; }

    /// <summary>The trail from the sanitized text back to the raw one; sealed on its way to the database.</summary>
    public required string Trail { get; init; }

    /// <summary>How many edits each step made, in the order of the steps.</summary>
    public required IReadOnlyList<int> Counts { get; init; }

    /// <summary>How many occurrences each step left untreated, in the order of the steps.</summary>
    public required IReadOnlyList<int> Untreated { get; init; }

    /// <summary>Whether the text is, ordinal, the one the item holds; then it is not written again.</summary>
    public required bool Identical { get; init; }

    /// <summary>The version, the counts and whether it is identical; never the text nor the trail.</summary>
    public override string ToString() => $"Resanitized {{ Version = {Version}, Counts = {string.Join(", ", Counts)}, Identical = {Identical} }}";
}
