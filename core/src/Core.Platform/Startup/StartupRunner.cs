namespace Core.Platform.Startup;

/// <summary>Runs the checks in the order given and stops at the first one that fails.</summary>
public sealed class StartupRunner
{
    private readonly IReadOnlyList<IStartupCheck> checks;
    private readonly TimeProvider clock;

    /// <summary>The checks, in the order they run, and the clock that dates the report.</summary>
    public StartupRunner(IReadOnlyList<IStartupCheck> checks, TimeProvider clock)
    {
        this.checks = checks;
        this.clock = clock;
    }

    /// <summary>Runs until a check fails or none is left, and reports every check that ran.</summary>
    public async Task<StartupReport> RunAsync(CancellationToken cancellationToken)
    {
        List<CheckResult> results = [];
        var failed = false;
        for (var index = 0; index < checks.Count && !failed; index++)
        {
            var result = await checks[index].RunAsync(cancellationToken).ConfigureAwait(false);
            results.Add(result);
            failed = result.Status == CheckStatus.Failed;
        }

        return new StartupReport(results, clock.GetUtcNow());
    }
}
