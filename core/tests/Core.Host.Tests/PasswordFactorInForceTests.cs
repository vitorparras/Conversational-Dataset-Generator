using System.Text.Json;

namespace Core.Host.Tests;

/// <summary>The running host shows the work factor of the derivation of passwords in the configuration in force, with the origin that won (#69, CA-5).</summary>
public sealed class PasswordFactorInForceTests
{
    private const string Key = "Passwords:WorkFactor";
    private const string Factor = "7";

    [Fact]
    public async Task FactorGivenAsAnArgumentIsShownWithTheValueAndTheArgumentAsOrigin()
    {
        var address = HostProcess.FreeAddress();
        var installation = HostInstallation.Built(address) with { DeclaresWorkFactor = false };
        installation = installation with { Arguments = [.. installation.Arguments, $"--{Key}={Factor}"] };

        var shown = await ShownAsync(installation, address);

        Assert.Equal((Factor, "Argument"), shown);
    }

    [Fact]
    public async Task FactorGivenInTheEnvironmentIsShownWithTheValueAndTheEnvironmentAsOrigin()
    {
        var address = HostProcess.FreeAddress();
        var installation = HostInstallation.Built(address) with
        {
            DeclaresWorkFactor = false,
            Environment = new Dictionary<string, string> { ["CDG_Passwords__WorkFactor"] = Factor },
        };

        var shown = await ShownAsync(installation, address);

        Assert.Equal((Factor, "Environment"), shown);
    }

    [Fact]
    public async Task FactorGivenInTheSettingsFileIsShownWithTheValueAndTheFileAsOrigin()
    {
        var address = HostProcess.FreeAddress();
        using var copy = new HostCopy(/*lang=json*/ $$"""{ "Passwords": { "WorkFactor": "{{Factor}}" } }""");
        var installation = copy.Installation(HostInstallation.UrlsArgument(address)) with { DeclaresWorkFactor = false };

        var shown = await ShownAsync(installation, address);

        Assert.Equal((Factor, "File"), shown);
    }

    [Fact]
    public async Task ArgumentWinsOverTheEnvironmentAndTheOriginSaysSo()
    {
        var address = HostProcess.FreeAddress();
        var installation = HostInstallation.Built(address) with
        {
            DeclaresWorkFactor = false,
            Environment = new Dictionary<string, string> { ["CDG_Passwords__WorkFactor"] = "3" },
        };
        installation = installation with { Arguments = [.. installation.Arguments, $"--{Key}={Factor}"] };

        var shown = await ShownAsync(installation, address);

        Assert.Equal((Factor, "Argument"), shown);
    }

    private static async Task<(string? Value, string? Origin)> ShownAsync(HostInstallation installation, Uri address)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(installation, address, HostProcess.StartupTimeout, cancellationToken);

        using var response = await host.GetAsync("/configuration", cancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        var entry = body.RootElement.GetProperty("entries").EnumerateArray().Single(candidate => candidate.GetProperty("key").GetString() == Key);
        return (entry.GetProperty("value").GetString(), entry.GetProperty("origin").GetString());
    }
}
