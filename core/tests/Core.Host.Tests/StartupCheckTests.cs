using System.Text.Json;
using Core.Platform.Startup;

namespace Core.Host.Tests;

/// <summary>A resource the installation must carry is checked before the port opens; its absence stops the process and names it.</summary>
public sealed class StartupCheckTests
{
    private const string MissingFile = "ZQX-MARKER-7731.bin";

    [Fact]
    public async Task MissingBundledFileEndsTheProcessWithTheCheckCodeNamingTheItemAndItsSource()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var copy = new HostCopy(/*lang=json*/ "{}");

        var exit = await HostProcess.RunToExitAsync(
            copy.Installation($"--Host:BundledFile={MissingFile}"), HostProcess.StartupTimeout, cancellationToken);

        Assert.Equal((int)ExitCode.StartupCheckFailed, exit.Code);
        Assert.Contains("'Host:BundledFile'", exit.Error, StringComparison.Ordinal);
        Assert.Contains($"origem esperada: {MissingFile}", exit.Error, StringComparison.Ordinal);
    }

    [Trait("Category", "RealService")]
    [Fact]
    public async Task InstalledHostListsTheTargetAndTheBundledFileAsPassedAndIsReady()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var address = HostProcess.FreeAddress();
        var installation = HostInstallation.Built(address) with { UsesRealService = true };
        using var host = await HostProcess.StartAsync(installation, address, HostProcess.StartupTimeout, cancellationToken);

        using var response = await host.GetAsync("/status", cancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        Assert.True(body.RootElement.GetProperty("ready").GetBoolean());
        var checks = StartupCheckLog.Of(await host.StopAsync());
        Assert.Equal("RuntimeIdentifier", checks[0].Name);
        Assert.Equal("Passed", checks[0].Outcome);
        Assert.Equal("Host:BundledFile", checks[1].Name);
        Assert.Equal("Passed", checks[1].Outcome);
        Assert.Equal("MasterKey", checks[2].Name);
        Assert.Equal("Passed", checks[2].Outcome);
        Assert.Equal("Tls", checks[3].Name);
        Assert.Equal("Passed", checks[3].Outcome);
        Assert.Equal("Privilege", checks[4].Name);
        Assert.Equal("ServerLog", checks[16].Name);
        Assert.Equal("Passed", checks[16].Outcome);
        Assert.Equal("LanguageServiceFolder", checks[17].Name);
        Assert.Equal("Passed", checks[17].Outcome);
        Assert.Equal("LanguageService", checks[18].Name);
        Assert.Equal("Passed", checks[18].Outcome);
    }
}
