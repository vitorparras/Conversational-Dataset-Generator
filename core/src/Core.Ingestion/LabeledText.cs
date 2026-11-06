namespace Core.Ingestion;

/// <summary>A text of a labeled sequence and the passages the annotation falls on.</summary>
/// <param name="Text">The text.</param>
/// <param name="Passages">The passages, each inside the text; at least one.</param>
public sealed record LabeledText(string Text, IReadOnlyList<Passage> Passages)
{
    /// <summary>Only how many passages there are.</summary>
    public override string ToString() => $"LabeledText {{ Passages = {Passages.Count} }}";
}
