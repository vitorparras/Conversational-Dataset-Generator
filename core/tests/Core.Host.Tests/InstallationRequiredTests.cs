using System.Text.Json;
using Core.Platform.Startup;

namespace Core.Host.Tests;

/// <summary>The process does not start without declaring what kind of installation it is, and a demonstration that starts says so on its status.</summary>
public sealed class InstallationRequiredTests
{
    [Fact]
    public async Task ProcessWithoutTheKindEndsWithTheRefusalCodeAndNamesTheKey()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var installation = HostInstallation.Built(HostProcess.FreeAddress()) with { DeclaresInstallation = false };

        var exit = await HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, cancellationToken);

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("'Installation:Kind'", exit.Error, StringComparison.Ordinal);
        Assert.Contains("não tem valor padrão", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessOfADemonstrationAnswersItsKindOnTheStatus()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(cancellationToken);

        using var response = await host.GetAsync("/status", cancellationToken);
        using var status = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        Assert.Equal("demonstracao", status.RootElement.GetProperty("installation").GetProperty("kind").GetString());
    }

    [Fact]
    public async Task ProcessOfADemonstrationAnswersTheMarkedNameOnTheVersions()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(cancellationToken);

        using var response = await host.GetAsync("/versions", cancellationToken);
        using var versions = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        Assert.Contains("(DEMONSTRAÇÃO)", versions.RootElement.GetProperty("installation").GetProperty("name").GetString(), StringComparison.Ordinal);
    }
}
