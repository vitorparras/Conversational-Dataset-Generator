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

    [Fact]
    public async Task InstalledHostListsTheBundledFileAsPassedAndIsReady()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var address = HostProcess.FreeAddress();
        var installation = HostInstallation.Built(address) with { UsesRealService = true };
        using var host = await HostProcess.StartAsync(installation, address, HostProcess.StartupTimeout, cancellationToken);

        using var response = await host.GetAsync("/status", cancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        Assert.True(body.RootElement.GetProperty("ready").GetBoolean());
        var checks = body.RootElement.GetProperty("checks").EnumerateArray().ToList();
        Assert.Equal("Host:BundledFile", checks[0].GetProperty("name").GetString());
        Assert.Equal("Passed", checks[0].GetProperty("status").GetString());
        Assert.Equal("Database", checks[1].GetProperty("name").GetString());
        Assert.Equal("LanguageService", checks[2].GetProperty("name").GetString());
        Assert.Equal("Passed", checks[2].GetProperty("status").GetString());
    }
}
