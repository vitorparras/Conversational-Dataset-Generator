using Core.Persistence.Content.Writing;

namespace Core.Persistence.Content.Rerun;

/// <summary>What the rerun of an item works from: the raw text, the occurrences decided when it came in, and the sanitized text and version it holds, if any.</summary>
/// <param name="Raw">The raw text.</param>
/// <param name="Occurrences">The occurrences and their destinations; null when the destinations were never decided.</param>
/// <param name="StoredText">The sanitized text held.</param>
/// <param name="StoredVersion">Its version.</param>
public sealed record RerunSource(string Raw, IReadOnlyList<ItemOccurrence>? Occurrences, string? StoredText, string? StoredVersion)
{
    /// <summary>The item rerun.</summary>
    public long ItemId { get; init; }

    /// <summary>The entries of the map the item holds, opened; null when the item is not pseudonymized.</summary>
    public IReadOnlyList<NewMapEntry>? Map { get; init; }

    /// <summary>The markers another batch holds, met by the draws before this one; a marker new to the item is none of them.</summary>
    public IReadOnlySet<string> Taken { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>The version and how many occurrences; never a text.</summary>
    public override string ToString() => $"RerunSource {{ StoredVersion = {StoredVersion}, Occurrences = {Occurrences?.Count} }}";
}
