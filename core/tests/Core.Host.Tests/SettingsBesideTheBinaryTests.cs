using System.Net;

namespace Core.Host.Tests;

/// <summary>The host reads the settings file beside its binary, not one in the directory it was started from.</summary>
public sealed class SettingsBesideTheBinaryTests
{
    [Fact]
    public async Task SettingsFileBesideTheBinaryIsReadWhateverTheWorkingDirectory()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var address = HostProcess.FreeAddress();
        using var copy = new HostCopy($$"""{ "Host": { "Urls": "{{address.GetLeftPart(UriPartial.Authority)}}" } }""");
        using var elsewhere = new TemporaryDirectory();
        var installation = copy.Installation() with { WorkingDirectory = elsewhere.Path };

        // No argument gives the address, and the default is another one: the host answers here only because it read the file beside its binary.
        using var host = await HostProcess.StartAsync(installation, address, HostProcess.StartupTimeout, cancellationToken);
        using var response = await host.GetAsync("/status", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
