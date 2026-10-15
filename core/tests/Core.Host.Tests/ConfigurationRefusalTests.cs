using Core.Platform.Startup;

namespace Core.Host.Tests;

/// <summary>A settings file the host does not recognise keeps it from starting, and the refusal names the key.</summary>
public sealed class ConfigurationRefusalTests
{
    [Fact]
    public async Task UnknownKeyInTheSettingsFileEndsTheProcessWithTheRefusalCodeAndNamesTheKey()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var settings = new SettingsDirectory(/*lang=json*/ """{ "Host": { "Treshold": "3" } }""");

        var exit = await HostProcess.RunToExitAsync(settings.Path, cancellationToken);

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("'Host:Treshold'", exit.Error, StringComparison.Ordinal);
        Assert.Contains("a configuração foi recusada", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnreadableSettingsFileEndsTheProcessWithTheRefusalCodeAndNamesTheFile()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var settings = new SettingsDirectory("{ not json");

        var exit = await HostProcess.RunToExitAsync(settings.Path, cancellationToken);

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("'appsettings.json'", exit.Error, StringComparison.Ordinal);
    }
}
