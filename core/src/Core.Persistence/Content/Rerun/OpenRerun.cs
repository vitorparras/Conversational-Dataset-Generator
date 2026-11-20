namespace Core.Persistence.Content.Rerun;

/// <summary>A request with an item not concluded, and the version it reruns to.</summary>
/// <param name="RerunId">The request.</param>
/// <param name="TargetVersion">The version it reruns to.</param>
public sealed record OpenRerun(long RerunId, string TargetVersion);
