using Core.Platform.Startup;

namespace Core.Host.Tests;

/// <summary>The refusal names the check that failed and where it should come from, and never one that passed.</summary>
public sealed class StartupRefusalTests
{
    [Fact]
    public void RefusalNamesTheFailedCheckAndItsSourceNotThePassedOneAndReturnsTheCheckCode()
    {
        var report = new StartupReport(
        [
            CheckResult.Passed("first", "recurso presente", "first-source"),
            CheckResult.Failed("second", "recurso ausente", "second-source"),
        ]);
        using var output = new StringWriter();

        var code = StartupRefusal.Report(output, report);

        var text = output.ToString();
        Assert.Equal(ExitCode.StartupCheckFailed, code);
        Assert.Contains("second", text, StringComparison.Ordinal);
        Assert.Contains("second-source", text, StringComparison.Ordinal);
        Assert.DoesNotContain("first", text, StringComparison.Ordinal);
    }
}
