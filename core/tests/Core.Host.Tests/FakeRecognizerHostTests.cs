using System.Text.Json;
using Core.Platform.Startup;

namespace Core.Host.Tests;

/// <summary>
/// The built host with the fake recognizer starts without the language service, which none of its startup checks asks (their lines in the log say which ran, since the detail of
/// <c>/status</c> needs a session, #632); a kind outside the set keeps it from starting.
/// </summary>
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
        var names = StartupCheckLog.Of(await host.StopAsync()).Select(check => check.Name);
        Assert.Equal(
            [
                "RuntimeIdentifier", "Host:BundledFile", "MasterKey", "Tls", "Privilege", "OwnerWindow", "RlsForce", "RestoreWindow", "Serializable", "PgAudit", "ColumnRevoke",
                "SchemaVersion", "Immutability", "ProtectedColumns", "DerivationLabels", "ContentEpochs", "ServerLog"
            ], names);
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
