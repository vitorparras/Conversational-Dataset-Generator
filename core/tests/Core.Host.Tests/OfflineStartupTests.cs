using System.Text.Json;

namespace Core.Host.Tests;

/// <summary>
/// With every proxy variable pointing at a proxy that never answers, the host and the service it starts come up ready,
/// and no connection reaches the proxy: nothing on the way up waits on the network.
/// </summary>
public sealed class OfflineStartupTests
{
    [Fact]
    public async Task HostBecomesReadyWithoutReachingTheProxyItsEnvironmentNames()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var proxy = new SilentProxy();
        var address = HostProcess.FreeAddress();
        var installation = HostInstallation.Built(address) with { Environment = proxy.Environment };
        using var host = await HostProcess.StartAsync(installation, address, HostProcess.StartupTimeout, cancellationToken);

        using var response = await host.GetAsync("/status", cancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        Assert.True(body.RootElement.GetProperty("ready").GetBoolean());
        Assert.False(proxy.WasReached);
    }
}
