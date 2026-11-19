using Core.Ingestion.Deciding;

namespace Core.Ingestion.Sanitizing;

/// <summary>What a step receives: the text the earlier steps left and the occurrences still in it, with their positions in that text.</summary>
/// <param name="Text">The text as the earlier steps left it.</param>
/// <param name="Occurrences">The decided occurrences no earlier step removed, in code points of this text.</param>
public sealed record StepInput(string Text, IReadOnlyList<DecidedOccurrence> Occurrences)
{
    /// <summary>How many occurrences; never the text.</summary>
    public override string ToString() => $"StepInput {{ Occurrences = {Occurrences.Count} }}";
}
