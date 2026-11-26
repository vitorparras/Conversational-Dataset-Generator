namespace Core.Persistence.Content.Rerun;

/// <summary>How many items of accepted batches, not emptied, a version holds.</summary>
/// <param name="Version">The version; none for the items written before the sanitization.</param>
/// <param name="Items">How many.</param>
/// <param name="WithoutMap">How many of them hold no map, stored before the coreference, which a rerun to this very version still queues (issue 102, F3).</param>
public sealed record VersionItems(string? Version, long Items, long WithoutMap);
