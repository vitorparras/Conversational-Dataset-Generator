using System.Net;
using System.Text.Json;
using Core.Host.Tests.Supervision;

namespace Core.Host.Tests;

/// <summary>The built host starts and answers the status route, which follows the language service it supervises.</summary>
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
        Assert.Equal("http", body.RootElement.GetProperty("recognizer").GetString());
        Assert.Equal("Available", body.RootElement.GetProperty("languageService").GetString());
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
        using var body = await StatusAsync(host, cancellationToken);

        Assert.True(notReady, "the status still said ready after the service process was killed");
        Assert.Equal("Unavailable", body.RootElement.GetProperty("languageService").GetString());
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
