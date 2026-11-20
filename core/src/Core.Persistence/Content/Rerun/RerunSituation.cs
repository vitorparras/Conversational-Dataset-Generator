namespace Core.Persistence.Content.Rerun;

/// <summary>The items of a domain by version, the most recent first, and the newest open request, none when no request is open.</summary>
/// <param name="Versions">The items by version.</param>
/// <param name="Open">The newest open request.</param>
public sealed record RerunSituation(IReadOnlyList<VersionItems> Versions, OpenRerun? Open);
