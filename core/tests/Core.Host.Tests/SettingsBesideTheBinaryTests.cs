using System.Text.Json;

namespace Core.Host.Tests;

/// <summary>The host reads the settings file beside its binary, not one in the directory it was started from.</summary>
public sealed class SettingsBesideTheBinaryTests
{
    [Fact]
    public async Task SettingsFileBesideTheBinaryIsReadWhateverTheWorkingDirectory()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var address = HostProcess.FreeAddress();
        var authority = address.GetLeftPart(UriPartial.Authority);
        using var copy = new HostCopy($$"""{ "Host": { "Urls": "{{authority}}" } }""");
        using var elsewhere = new TemporaryDirectory();
        var installation = copy.Installation() with { WorkingDirectory = elsewhere.Path };

        using var host = await HostProcess.StartAsync(installation, address, HostProcess.StartupTimeout, cancellationToken);
        using var response = await host.GetAsync("/configuration", cancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        var urls = body.RootElement.GetProperty("entries").EnumerateArray()
            .Single(entry => entry.GetProperty("key").GetString() == "Host:Urls");
        Assert.Equal(authority, urls.GetProperty("value").GetString());
        Assert.Equal("File", urls.GetProperty("origin").GetString());
    }
}
