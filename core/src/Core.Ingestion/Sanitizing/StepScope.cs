namespace Core.Ingestion.Sanitizing;

/// <summary>What a step alters and what it leaves untouched, in words a reviewer reads.</summary>
/// <param name="Alters">What the step changes in the text.</param>
/// <param name="Leaves">What the step leaves as it came.</param>
public sealed record StepScope(string Alters, string Leaves)
{
    /// <summary>Both halves are said; a blank half is no declaration.</summary>
    public bool IsDeclared => !string.IsNullOrWhiteSpace(Alters) && !string.IsNullOrWhiteSpace(Leaves);
}
