namespace Core.Persistence.Content.Rerun;

/// <summary>How many items of accepted batches, not emptied, a version holds.</summary>
/// <param name="Version">The version; none for the items written before the sanitization.</param>
/// <param name="Items">How many.</param>
public sealed record VersionItems(string? Version, long Items);
