namespace Core.Platform.Startup;

/// <summary>Every startup check, in the order it ran, with its result.</summary>
public sealed record StartupReport(IReadOnlyList<CheckResult> Results)
{
    /// <summary>Whether every check passed. A process is ready to serve only then.</summary>
    public bool AllPassed => Results.All(result => result.Status == CheckStatus.Passed);
}
