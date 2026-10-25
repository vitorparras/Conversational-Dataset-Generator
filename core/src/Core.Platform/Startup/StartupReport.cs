namespace Core.Platform.Startup;

/// <summary>What the checks that ran concluded, in the order they ran, and when the last of them ended.</summary>
public sealed record StartupReport(IReadOnlyList<CheckResult> Results, DateTimeOffset CheckedAt)
{
    /// <summary>Whether no check that ran failed.</summary>
    public bool Ready => Results.All(result => result.Status != CheckStatus.Failed);

    /// <summary>The first check that failed, or null when none did.</summary>
    public CheckResult? FirstFailure => Results.FirstOrDefault(result => result.Status == CheckStatus.Failed);
}
