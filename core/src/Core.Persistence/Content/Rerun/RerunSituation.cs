namespace Core.Persistence.Content.Rerun;

/// <summary>The items of the accepted batches of a domain by version, the most recent first, and the newest open request with its version.</summary>
/// <param name="Versions">The items by version.</param>
/// <param name="OpenRerunId">The newest open request; none when none is open.</param>
/// <param name="OpenVersion">The version it reruns to.</param>
public sealed record RerunSituation(IReadOnlyList<VersionItems> Versions, long? OpenRerunId, string? OpenVersion);
