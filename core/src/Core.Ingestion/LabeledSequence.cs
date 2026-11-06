namespace Core.Ingestion;

/// <summary>Text where the annotation falls on passages and not on the whole.</summary>
public sealed class LabeledSequence : Material
{
    /// <summary>The sequence holds its texts with their passages; only the conference of the form makes one.</summary>
    /// <param name="texts">The texts with their passages.</param>
    internal LabeledSequence(IReadOnlyList<LabeledText> texts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        Texts = texts;
    }

    /// <inheritdoc />
    public override MaterialForm Form => MaterialForm.LabeledSequence;

    /// <summary>The texts with their passages.</summary>
    public IReadOnlyList<LabeledText> Texts { get; }

    /// <summary>Only how many texts there are.</summary>
    public override string ToString() => $"LabeledSequence {{ Texts = {Texts.Count} }}";
}
