namespace Core.Platform.Startup;

/// <summary>What the checks that ran concluded, in the order they ran.</summary>
public sealed record StartupReport(IReadOnlyList<CheckResult> Results)
{
    /// <summary>Whether every check that ran passed.</summary>
    public bool Ready => Results.All(result => result.Status == CheckStatus.Passed);

    /// <summary>The first check that failed, or null when none did.</summary>
    public CheckResult? FirstFailure => Results.FirstOrDefault(result => result.Status == CheckStatus.Failed);
}
