using System.Text.Json;
using Core.Platform.Startup;

namespace Core.Host.Tests;

/// <summary>The built host with the fake recognizer starts without the language service and shows the kind in force; a kind outside the set keeps it from starting.</summary>
public sealed class FakeRecognizerHostTests
{
    [Fact]
    public async Task HostWithTheFakeIsReadyWithoutCheckingTheLanguageService()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(cancellationToken);

        using var response = await host.GetAsync("/status", cancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        Assert.True(body.RootElement.GetProperty("ready").GetBoolean());
        Assert.Equal("fake", body.RootElement.GetProperty("recognizer").GetString());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("languageService").ValueKind);
        var names = body.RootElement.GetProperty("checks").EnumerateArray().Select(check => check.GetProperty("name").GetString()).ToList();
        Assert.Equal(["RuntimeIdentifier", "Host:BundledFile", "MasterKey", "Tls", "Database"], names);
    }

    [Fact]
    public async Task HostWithAKindOutsideTheSetEndsWithTheRefusalCodeNamingTheKeyAndTheAcceptedValues()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var copy = new HostCopy(/*lang=json*/ "{}");
        var arguments = (string[])["--Recognizer:Kind=Fake", "--LanguageService:Executable=zqx-missing-7731"];
        var installation = copy.Installation(arguments) with { ProvidesLanguageService = false };

        var exit = await HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, cancellationToken);

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("'Recognizer:Kind': encontrado 'Fake'; aceitos: fake, http", exit.Error, StringComparison.Ordinal);
    }
}
