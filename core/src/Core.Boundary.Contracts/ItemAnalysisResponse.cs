namespace Core.Boundary.Contracts;

/// <summary>What the language service answers for a whole item: an empty list of occurrences means the item was analysed and nothing was found.</summary>
/// <param name="Occurrences">The occurrences found, each in its own text and with its group.</param>
/// <param name="AuthorGroups">The group of each distinct author named in the request, one for each.</param>
/// <param name="ModelVersion">The version of the model behind the answer; null when there is no model.</param>
public sealed record ItemAnalysisResponse(IReadOnlyList<GroupedOccurrence> Occurrences, IReadOnlyList<AuthorGroup> AuthorGroups, string? ModelVersion)
{
    /// <summary>How many occurrences and authors and which model; never an offset nor a name.</summary>
    public override string ToString() =>
        $"ItemAnalysisResponse {{ Occurrences = {Occurrences.Count}, AuthorGroups = {AuthorGroups.Count}, ModelVersion = {ModelVersion} }}";
}
