namespace Core.Persistence.Content.Rerun;

/// <summary>What the rerun of an item works from: the raw text, the occurrences decided when it came in, and the sanitized text and the version it holds.</summary>
/// <param name="Raw">The raw text.</param>
/// <param name="Occurrences">The occurrences, with the destination decided when the item came in.</param>
/// <param name="StoredText">The sanitized text the item holds; none for an item written before the sanitization.</param>
/// <param name="StoredVersion">The version of the steps that produced it; none with it.</param>
public sealed record RerunSource(string Raw, IReadOnlyList<ItemOccurrence> Occurrences, string? StoredText, string? StoredVersion)
{
    /// <summary>The version and how many occurrences; never a text.</summary>
    public override string ToString() => $"RerunSource {{ StoredVersion = {StoredVersion}, Occurrences = {Occurrences.Count} }}";
}
