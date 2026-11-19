namespace Core.Ingestion.Sanitizing;

/// <summary>What one step changed: its name, its version and its edits, whose count is what the batch shows.</summary>
/// <param name="Name">The name the step declares.</param>
/// <param name="Version">The version the step declares.</param>
/// <param name="Edits">The edits the step returned, in the positions of the text it received.</param>
public sealed record StepReport(string Name, int Version, IReadOnlyList<SanitizationEdit> Edits)
{
    /// <summary>How many edits the step made, zero included.</summary>
    public int Count => Edits.Count;

    /// <summary>How many spans the step left as written because it could not tell what to do with them; a number, never the span.</summary>
    public int Untreated { get; init; }

    /// <summary>The name, version and counts; never the positions.</summary>
    public override string ToString() => $"StepReport {{ Name = {Name}, Version = {Version}, Count = {Count}, Untreated = {Untreated} }}";
}
