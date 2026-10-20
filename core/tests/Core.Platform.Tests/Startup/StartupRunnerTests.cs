using Core.Platform.Startup;
using Core.Platform.Tests.Logging;

namespace Core.Platform.Tests.Startup;

/// <summary>The checks run in the order given, stop at the first failure, and only a fully green run is ready.</summary>
public sealed class StartupRunnerTests
{
    [Fact]
    public async Task ChecksRunInTheOrderGivenAndEveryResultIsReported()
    {
        List<string> ran = [];
        var runner = new StartupRunner(
        [
            new ScriptedStartupCheck("first", CheckStatus.Passed, ran),
            new ScriptedStartupCheck("second", CheckStatus.Passed, ran),
            new ScriptedStartupCheck("third", CheckStatus.Passed, ran),
        ], TimeProvider.System);

        var report = await runner.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["first", "second", "third"], ran);
        Assert.Equal(["first", "second", "third"], report.Results.Select(result => result.Name));
        Assert.True(report.Ready);
    }

    [Fact]
    public async Task RunStopsAtTheFirstFailureAndLaterChecksNeverRun()
    {
        List<string> ran = [];
        var runner = new StartupRunner(
        [
            new ScriptedStartupCheck("first", CheckStatus.Passed, ran),
            new ScriptedStartupCheck("second", CheckStatus.Failed, ran),
            new ScriptedStartupCheck("third", CheckStatus.Passed, ran),
        ], TimeProvider.System);

        var report = await runner.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["first", "second"], ran);
        Assert.False(report.Ready);
        Assert.Equal("second", report.FirstFailure?.Name);
    }

    [Fact]
    public async Task RunWithoutChecksIsReadyAndHasNoFailure()
    {
        var runner = new StartupRunner([], TimeProvider.System);

        var report = await runner.RunAsync(TestContext.Current.CancellationToken);

        Assert.True(report.Ready);
        Assert.Null(report.FirstFailure);
    }

    [Fact]
    public async Task ReportIsDatedByTheClockGiven()
    {
        var checkedAt = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
        var runner = new StartupRunner([new ScriptedStartupCheck("first", CheckStatus.Passed, [])], new FixedTimeProvider(checkedAt));

        var report = await runner.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(checkedAt, report.CheckedAt);
    }

    [Fact]
    public async Task NotConfiguredCheckNeitherFailsNorStopsTheRun()
    {
        List<string> ran = [];
        var runner = new StartupRunner(
        [
            new ScriptedStartupCheck("first", CheckStatus.NotConfigured, ran),
            new ScriptedStartupCheck("second", CheckStatus.Passed, ran),
        ], TimeProvider.System);

        var report = await runner.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["first", "second"], ran);
        Assert.True(report.Ready);
        Assert.Null(report.FirstFailure);
    }
}
