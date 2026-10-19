namespace Core.Boundary.Contracts;

/// <summary>What the language service answers: an empty list means the text was analysed and nothing was found.</summary>
/// <param name="Occurrences">The occurrences found in the text.</param>
/// <param name="ModelVersion">The version of the model behind the answer; null when there is no model.</param>
public sealed record AnalysisResponse(IReadOnlyList<Occurrence> Occurrences, string? ModelVersion)
{
    /// <summary>How many occurrences and which model; never an offset.</summary>
    public override string ToString() =>
        $"AnalysisResponse {{ Occurrences = {Occurrences.Count}, ModelVersion = {ModelVersion} }}";
}
