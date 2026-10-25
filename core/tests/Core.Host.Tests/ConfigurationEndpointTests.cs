using System.Net;
using System.Text.Json;

namespace Core.Host.Tests;

/// <summary>The running host shows the configuration in force with the origin that won for each key.</summary>
public sealed class ConfigurationEndpointTests
{
    [Fact]
    public async Task ConfigurationInForceShowsTheKeyWithTheValueAndOriginThatWon()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(cancellationToken);

        using var response = await host.GetAsync("/configuration", cancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var urls = body.RootElement.GetProperty("entries").EnumerateArray()
            .Single(entry => entry.GetProperty("key").GetString() == "Host:Urls");
        Assert.Equal(host.BaseAddress.GetLeftPart(UriPartial.Authority), urls.GetProperty("value").GetString());
        Assert.Equal("Argument", urls.GetProperty("origin").GetString());
    }

    [Fact]
    public async Task OptionalKeyNoSourceWritesIsShownWithoutAValue()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(cancellationToken);

        using var response = await host.GetAsync("/configuration", cancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        var logFile = body.RootElement.GetProperty("entries").EnumerateArray()
            .Single(entry => entry.GetProperty("key").GetString() == "Host:LogFile");
        Assert.Equal(JsonValueKind.Null, logFile.GetProperty("value").ValueKind);
        Assert.Equal("Default", logFile.GetProperty("origin").GetString());
    }
}
