using System.Net;
using System.Text.Json;

namespace Core.Tests.Unit.Host;

/// <summary>Issue 24 CA-1: the built host starts and answers the status route.</summary>
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
}
