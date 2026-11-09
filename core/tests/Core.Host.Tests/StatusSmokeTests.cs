using System.Net;
using System.Text.Json;
using Core.Host.Tests.Supervision;

namespace Core.Host.Tests;

/// <summary>
/// The built host starts and answers the status route, whose readiness follows the language service it supervises; with no session the route says alive and ready alone (#632),
/// so the ready of the http recognizer stands for the service available.
/// </summary>
public sealed class StatusSmokeTests
{
    [Fact]
    public async Task StatusReportsTheProcessAlive()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(cancellationToken);

        using var response = await host.GetAsync("/status", cancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.RootElement.GetProperty("alive").GetBoolean());
    }

    [Trait("Category", "RealService")]
    [Fact]
    public async Task StatusReportsTheServiceAvailableAndTheProcessReady()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartWithTheRealServiceAsync(cancellationToken);

        using var body = await StatusAsync(host, cancellationToken);

        Assert.True(body.RootElement.GetProperty("ready").GetBoolean());
    }

    [Trait("Category", "RealService")]
    [Fact]
    public async Task KillingTheServiceProcessTurnsTheStatusNotReady()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartWithTheRealServiceAsync(cancellationToken);
        var tree = await ProcessTree.TreeOfAsync(host.Id, cancellationToken);
        var service = tree.First(line => line.CommandLine.Contains("language_service", StringComparison.Ordinal));

        StandInService.Kill(service.Id);
        var notReady = await Poll.UntilAsync(async () => !await IsReadyAsync(host, cancellationToken), Poll.LossLimit, cancellationToken);

        Assert.True(notReady, "the status still said ready after the service process was killed");
    }

    private static async Task<JsonDocument> StatusAsync(HostProcess host, CancellationToken cancellationToken)
    {
        using var response = await host.GetAsync("/status", cancellationToken);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    private static async Task<bool> IsReadyAsync(HostProcess host, CancellationToken cancellationToken)
    {
        using var body = await StatusAsync(host, cancellationToken);
        return body.RootElement.GetProperty("ready").GetBoolean();
    }
}
