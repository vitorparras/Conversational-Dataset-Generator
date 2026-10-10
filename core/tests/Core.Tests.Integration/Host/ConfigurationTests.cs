using System.Globalization;
using System.Text.Json;
using Core.Platform.Startup;

namespace Core.Tests.Integration.Host;

/// <summary>Issue 25 on the real process: a closed configuration refuses to start, and shows itself.</summary>
public sealed class ConfigurationTests
{
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task InventedKeyInTheFileRefusesToStartAndNamesTheKey()
    {
        using var installation = HostInstallation.Create();
        installation.WriteSettings("""{ "Server": { "Port": "0", "Prot": "5081" } }""");
        await using var host = installation.Start();

        var exitCode = await host.WaitForExitAsync(ExitTimeout);

        Assert.Equal(ExitCodes.ConfigurationRefused, exitCode);
        Assert.Contains("'Server:Prot'", host.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{ "Server": { "Port": "0" }, "Typo": null }""", "Typo")]
    [InlineData("""{ "Server": { "Port": "0" }, "Typo": {} }""", "Typo")]
    [InlineData("""{ "Server": { "Port": "0", "Prot": null } }""", "Server:Prot")]
    public async Task UnknownKeyWithANullOrEmptyValueRefusesToStartAndNamesTheKey(string settings, string key)
    {
        using var installation = HostInstallation.Create();
        installation.WriteSettings(settings);
        await using var host = installation.Start();

        var exitCode = await host.WaitForExitAsync(ExitTimeout);

        Assert.Equal(ExitCodes.ConfigurationRefused, exitCode);
        Assert.Contains($"chave desconhecida '{key}'", host.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValueOutsideTheFormatRefusesToStartAndSaysWhatWasFound()
    {
        using var installation = HostInstallation.Create();
        installation.WriteSettings("""{ "Server": { "Address": "localhost" } }""");
        await using var host = installation.Start();

        var exitCode = await host.WaitForExitAsync(ExitTimeout);

        Assert.Equal(ExitCodes.ConfigurationRefused, exitCode);
        Assert.Contains("'Server:Address'", host.Output, StringComparison.Ordinal);
        Assert.Contains("'localhost'", host.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfigurationEndpointShowsEveryValueWithTheOriginThatWon()
    {
        using var installation = HostInstallation.Create();
        var port = HostInstallation.FreePort();
        installation.WriteSettings("""{ "Server": { "Port": "1" } }""");
        await using var host = installation.Start(
            environment: new Dictionary<string, string?>
            {
                ["CDG__Server__Port"] = "2",
            },
            arguments: ["--Server:Port", port.ToString(CultureInfo.InvariantCulture)]);
        using var client = new HostClient(host, port);

        var view = await client.GetWhenUpAsync("/configuration");

        var entries = view.GetProperty("entries").EnumerateArray().ToDictionary(
            e => e.GetProperty("key").GetString()!,
            e => (Value: e.GetProperty("value").GetString(), Origin: e.GetProperty("origin").GetString()));
        Assert.Equal((port.ToString(CultureInfo.InvariantCulture), "argument"), entries["Server:Port"]);
        Assert.Equal(("127.0.0.1", "default"), entries["Server:Address"]);
    }

    [Fact]
    public async Task EnvironmentVariableBeatsTheFile()
    {
        using var installation = HostInstallation.Create();
        var port = HostInstallation.FreePort();
        installation.WriteSettings("""{ "Server": { "Port": "1" } }""");
        await using var host = installation.Start(
            environment: new Dictionary<string, string?>
            {
                ["CDG__Server__Port"] = port.ToString(CultureInfo.InvariantCulture),
            });
        using var client = new HostClient(host, port);

        var view = await client.GetWhenUpAsync("/configuration");

        var entry = view.GetProperty("entries").EnumerateArray()
            .Single(e => e.GetProperty("key").GetString() == "Server:Port");
        Assert.Equal("environment", entry.GetProperty("origin").GetString());
        Assert.Equal(JsonValueKind.False, entry.GetProperty("redacted").ValueKind);
    }
}
