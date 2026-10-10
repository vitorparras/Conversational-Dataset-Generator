using System.Diagnostics;
using System.Globalization;
using Core.Platform.Startup;

namespace Core.Tests.Integration.Host;

/// <summary>Issues 24 and 26 on the real process: checks before the port, alive and ready, shutdown.</summary>
public sealed class StartupTests
{
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task MissingBundledResourceRefusesToStartNamingTheItemAndWhereItShouldComeFrom()
    {
        using var installation = HostInstallation.Create();
        installation.WriteSettings("""{ "Server": { "Port": "0" }, "Installation": { "BundledFiles": "models/zqx-model.bin" } }""");
        await using var host = installation.Start();

        var exitCode = await host.WaitForExitAsync(ExitTimeout);

        Assert.Equal(ExitCodes.StartupCheckFailed, exitCode);
        Assert.Contains("Conferência bundled-file:models/zqx-model.bin: reprovada", host.Output, StringComparison.Ordinal);
        Assert.Contains("recurso ausente 'models/zqx-model.bin'", host.Output, StringComparison.Ordinal);
        Assert.Contains("declarado em Installation:BundledFiles (arquivo)", host.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Núcleo ouvindo", host.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StatusListsEveryCheckWithItsResultAndIsReadyWhenAllPassed()
    {
        using var installation = HostInstallation.Create();
        var port = HostInstallation.FreePort();
        _ = installation.Directory.CreateSubdirectory("models");
        await File.WriteAllTextAsync(
            Path.Combine(installation.Directory.FullName, "models", "zqx-model.bin"), "x", TestContext.Current.CancellationToken);
        installation.WriteSettings(string.Create(
            CultureInfo.InvariantCulture,
            $$"""{ "Server": { "Port": "{{port}}" }, "Installation": { "BundledFiles": "models/zqx-model.bin" } }"""));
        await using var host = installation.Start();
        using var client = new HostClient(host, port);

        var status = await client.GetWhenUpAsync("/status");

        Assert.True(status.GetProperty("alive").GetBoolean());
        Assert.True(status.GetProperty("ready").GetBoolean());
        var check = Assert.Single(status.GetProperty("checks").EnumerateArray());
        Assert.Equal("bundled-file:models/zqx-model.bin", check.GetProperty("name").GetString());
        Assert.Equal("passed", check.GetProperty("status").GetString());
    }

    [Fact]
    public async Task StartsWithoutNetworkWithinFiveSeconds()
    {
        using var installation = HostInstallation.Create();
        var port = HostInstallation.FreePort();
        installation.WriteSettings(HostInstallation.SettingsWithPort(port));

        // Every proxy points at an address that never answers: an outbound call on the startup
        // path would hang here until its own timeout, far beyond the limit below.
        const string Blackhole = "http://10.255.255.1:9";
        var watch = Stopwatch.StartNew();
        await using var host = installation.Start(environment: new Dictionary<string, string?>
        {
            ["HTTP_PROXY"] = Blackhole,
            ["HTTPS_PROXY"] = Blackhole,
            ["ALL_PROXY"] = Blackhole,
            ["NO_PROXY"] = string.Empty,
        });
        using var client = new HostClient(host, port);

        var status = await client.GetWhenUpAsync("/status");
        watch.Stop();

        Assert.True(status.GetProperty("ready").GetBoolean());
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"startup took {watch.Elapsed}");
    }

    [Fact]
    public async Task RequestedShutdownWritesTheLastLineToEveryDestinationAndExitsWithZero()
    {
        using var installation = HostInstallation.Create();
        var port = HostInstallation.FreePort();
        installation.WriteSettings(string.Create(
            CultureInfo.InvariantCulture,
            $$"""{ "Server": { "Port": "{{port}}" }, "Logging": { "FilePath": "logs/core.log" } }"""));
        await using var host = installation.Start();
        using var client = new HostClient(host, port);
        _ = await client.GetWhenUpAsync("/status");

        await ShutdownSignal.SendAsync(host);
        var exitCode = await host.WaitForExitAsync(ExitTimeout);

        const string LastLine = "Encerramento concluído; código de saída 0";
        Assert.Equal(ExitCodes.Requested, exitCode);
        Assert.EndsWith(LastLine, LastNonEmptyLine(host.Output), StringComparison.Ordinal);
        var file = await File.ReadAllTextAsync(
            Path.Combine(installation.Directory.FullName, "logs", "core.log"), TestContext.Current.CancellationToken);
        Assert.EndsWith(LastLine, LastNonEmptyLine(file), StringComparison.Ordinal);
        Assert.Contains("Encerramento pedido", file, StringComparison.Ordinal);
    }

    private static string LastNonEmptyLine(string text) =>
        text.Split('\n').Select(line => line.TrimEnd('\r')).Last(line => line.Length > 0);
}
