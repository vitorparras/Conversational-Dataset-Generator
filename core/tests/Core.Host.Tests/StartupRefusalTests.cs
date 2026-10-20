using Core.Platform.Startup;
using Core.Platform.Versions;

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
        ], DateTimeOffset.UnixEpoch);
        using var output = new StringWriter();

        var code = StartupRefusal.Report(output, report);

        var text = output.ToString();
        Assert.Equal(ExitCode.StartupCheckFailed, code);
        Assert.Contains("second", text, StringComparison.Ordinal);
        Assert.Contains("second-source", text, StringComparison.Ordinal);
        Assert.DoesNotContain("first", text, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusalSaysWhatTheCheckObserved()
    {
        var failed = CheckResult.Failed("Sample", "recurso recusado", "Sample:Host") with { Observed = "ZQX-MARKER-7731 1.0" };
        using var output = new StringWriter();

        _ = StartupRefusal.Report(output, new StartupReport([failed], DateTimeOffset.UnixEpoch));

        Assert.Contains("(origem esperada: Sample:Host; observado: ZQX-MARKER-7731 1.0)", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecutablePublishedForAnotherTargetEndsWithTheCheckCodeNamingTargetAndMachine()
    {
        var check = new RuntimeTargetCheck(VersionValue.Present("ZQX-RID"), "linux-x64");
        var report = await new StartupRunner([check], TimeProvider.System).RunAsync(TestContext.Current.CancellationToken);
        using var output = new StringWriter();

        var code = StartupRefusal.Report(output, report);

        var text = output.ToString();
        Assert.Equal(ExitCode.StartupCheckFailed, code);
        Assert.Contains("RuntimeIdentifier: publicado para o alvo ZQX-RID, mas o destino em que roda é linux-x64", text, StringComparison.Ordinal);
        Assert.Contains("origem esperada: um executável publicado para linux-x64", text, StringComparison.Ordinal);
    }
}
