using Core.Platform.Startup;

namespace Core.Host.Tests;

/// <summary>The process does not start without declaring what kind of installation it is, and a demonstration that starts says so in its log (the detail of /status needs a session, #632).</summary>
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
    public async Task ProcessOfADemonstrationSaysItsKindInItsLog()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(cancellationToken);

        var output = await host.StopAsync();

        Assert.Contains("Core.Host[202] Instalação de demonstração", output, StringComparison.Ordinal);
    }
}
