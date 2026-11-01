using Core.Persistence.Tests.Fixtures;
using System.Net;
using System.Text.Json;
using Core.Platform.Configuration;

namespace Core.Tests.Integration.Hosts;

/// <summary>The running host, entered, shows the configuration in force with the origin that won for each key; the keys that hide their value show it hidden.</summary>
[Trait("Category", "Database")]
public sealed class ConfigurationInForceTests(DefaultHostFixture fixture) : IClassFixture<DefaultHostFixture>
{
    private const string Key = "Passwords:WorkFactor";
    private const string Factor = "7";

    [DatabaseFact]
    public async Task ConfigurationInForceShowsTheKeyWithTheValueAndOriginThatWon()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var response = await fixture.Host.GetAsync("/configuration", cancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var urls = EntryOf(body, "Host:Urls");
        Assert.Equal(fixture.Host.Address.GetLeftPart(UriPartial.Authority), urls.GetProperty("value").GetString());
        Assert.Equal("Argument", urls.GetProperty("origin").GetString());
    }

    [DatabaseFact]
    public async Task OptionalKeyNoSourceWritesIsShownWithoutAValue()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var response = await fixture.Host.GetAsync("/configuration", cancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        var logFile = EntryOf(body, "Host:LogFile");
        Assert.Equal(JsonValueKind.Null, logFile.GetProperty("value").ValueKind);
        Assert.Equal("Default", logFile.GetProperty("origin").GetString());
    }

    [DatabaseFact]
    public async Task ConfigurationInForceShowsTheFakeKindWithItsOrigin()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var response = await fixture.Host.GetAsync("/configuration", cancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        var kind = EntryOf(body, "Recognizer:Kind");
        Assert.Equal("fake", kind.GetProperty("value").GetString());
        Assert.Equal("Argument", kind.GetProperty("origin").GetString());
    }

    [DatabaseFact]
    public async Task FactorGivenAsAnArgumentIsShownWithTheValueAndTheArgumentAsOrigin()
    {
        var shown = await ShownAsync(new HostSetup { Arguments = [$"--{Key}={Factor}"], WorkFactor = int.Parse(Factor, System.Globalization.CultureInfo.InvariantCulture) });

        Assert.Equal((Factor, "Argument"), shown);
    }

    [DatabaseFact]
    public async Task FactorGivenInTheEnvironmentIsShownWithTheValueAndTheEnvironmentAsOrigin()
    {
        var setup = new HostSetup
        {
            Omit = [Key],
            Environment = new Dictionary<string, string> { ["CDG_Passwords__WorkFactor"] = Factor },
            WorkFactor = int.Parse(Factor, System.Globalization.CultureInfo.InvariantCulture),
        };

        var shown = await ShownAsync(setup);

        Assert.Equal((Factor, "Environment"), shown);
    }

    [DatabaseFact]
    public async Task FactorGivenInTheSettingsFileIsShownWithTheValueAndTheFileAsOrigin()
    {
        var setup = new HostSetup
        {
            Omit = [Key],
            SettingsJson = /*lang=json*/ $$"""{ "Passwords": { "WorkFactor": "{{Factor}}" } }""",
            WorkFactor = int.Parse(Factor, System.Globalization.CultureInfo.InvariantCulture),
        };

        var shown = await ShownAsync(setup);

        Assert.Equal((Factor, "File"), shown);
    }

    [DatabaseFact]
    public async Task ArgumentWinsOverTheEnvironmentAndTheOriginSaysSo()
    {
        var setup = new HostSetup
        {
            Arguments = [$"--{Key}={Factor}"],
            Environment = new Dictionary<string, string> { ["CDG_Passwords__WorkFactor"] = "3" },
            WorkFactor = int.Parse(Factor, System.Globalization.CultureInfo.InvariantCulture),
        };

        var shown = await ShownAsync(setup);

        Assert.Equal((Factor, "Argument"), shown);
    }

    [DatabaseFact]
    public async Task ConfigurationOfARealInstallationHidesThePathsAndTheHolderAndShowsTheDate()
    {
        var setup = new HostSetup
        {
            Arguments = ["--Installation:Kind=real", "--Custody:LastRecoveryTest=2026-10-01", "--Custody:Holder=PESSOA_1"],
        };
        await using var host = await AuthenticatedHost.StartAsync(setup, TestContext.Current.CancellationToken);

        using var response = await host.GetAsync("/configuration", TestContext.Current.CancellationToken);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var body = JsonDocument.Parse(text);

        var values = body.RootElement.GetProperty("entries").EnumerateArray()
            .ToDictionary(entry => entry.GetProperty("key").GetString()!, entry => entry.GetProperty("value").GetString(), StringComparer.Ordinal);
        Assert.Equal(EffectiveConfiguration.Redacted, values["MasterKey:File"]);
        Assert.Equal(EffectiveConfiguration.Redacted, values["MasterKey:LabelsFile"]);
        Assert.Equal(EffectiveConfiguration.Redacted, values["Custody:Holder"]);
        Assert.Equal("2026-10-01", values["Custody:LastRecoveryTest"]);
        Assert.DoesNotContain(Core.Tests.Support.Custody.CustodyFiles.Shared.Directory, text, StringComparison.Ordinal);
        Assert.DoesNotContain("PESSOA_1", text, StringComparison.Ordinal);
    }

    private static JsonElement EntryOf(JsonDocument body, string key) =>
        body.RootElement.GetProperty("entries").EnumerateArray().Single(entry => entry.GetProperty("key").GetString() == key);

    private static async Task<(string? Value, string? Origin)> ShownAsync(HostSetup setup)
    {
        await using var host = await AuthenticatedHost.StartAsync(setup, TestContext.Current.CancellationToken);

        using var response = await host.GetAsync("/configuration", TestContext.Current.CancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var entry = EntryOf(body, Key);
        return (entry.GetProperty("value").GetString(), entry.GetProperty("origin").GetString());
    }
}
