namespace Core.Ingestion;

/// <summary>A text of a labeled sequence and the passages the annotation falls on.</summary>
/// <param name="Text">The text.</param>
/// <param name="Passages">The passages, each inside the text; at least one.</param>
public sealed record LabeledText(string Text, IReadOnlyList<Passage> Passages)
{
    /// <summary>The text of one of the passages: a pure function of the text and the passage, the same every time it is asked.</summary>
    /// <param name="position">The position of the passage among the passages of the text.</param>
    public string Resolve(int position)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(position, Passages.Count);
        var passage = Passages[position];
        return Text[passage.Start..passage.End];
    }

    /// <summary>Only how many passages there are.</summary>
    public override string ToString() => $"LabeledText {{ Passages = {Passages.Count} }}";
}
