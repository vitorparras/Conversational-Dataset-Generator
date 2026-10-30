using Core.Boundary.Fake;
using Core.Host.Supervision;
using Core.Host.Tests.Logging;
using Core.Platform;
using Core.Platform.Startup;

namespace Core.Host.Tests;

/// <summary>A demonstration is told apart from a real installation by its name, on the screen (status and versions) and in the log; a real one is not marked.</summary>
[Collection("console")]
public sealed class InstallationVisibilityTests
{
    private const string Mark = "(DEMONSTRAÇÃO)";

    private static readonly StartupReport Passed =
        new([CheckResult.Passed("Sample:File", "recurso presente", "ZQX-MARKER-7731.bin")], DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task StatusOfADemonstrationShowsItsKindAndItsMarkedName()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StatusHost.StartAsync(Passed, new ServiceProcessWatch(), new Installation(InstallationKind.Demonstration), cancellationToken);

        using var status = await host.StatusAsync(cancellationToken);

        var installation = status.RootElement.GetProperty("installation");
        Assert.Equal("demonstracao", installation.GetProperty("kind").GetString());
        Assert.Contains(Mark, installation.GetProperty("name").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task StatusOfARealInstallationShowsItsKindAndNoMark()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StatusHost.StartAsync(Passed, new ServiceProcessWatch(), new Installation(InstallationKind.Real), cancellationToken);

        using var status = await host.StatusAsync(cancellationToken);

        var installation = status.RootElement.GetProperty("installation");
        Assert.Equal("real", installation.GetProperty("kind").GetString());
        Assert.DoesNotContain(Mark, installation.GetProperty("name").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task VersionsOfADemonstrationShowItsKindAndItsMarkedName()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await VersionsHost.StartAsync(
            new NoServiceVersionReader(), cancellationToken, installation: new Installation(InstallationKind.Demonstration));

        using var versions = await host.VersionsAsync(cancellationToken);

        var installation = versions.RootElement.GetProperty("installation");
        Assert.Equal("demonstracao", installation.GetProperty("kind").GetString());
        Assert.Contains(Mark, installation.GetProperty("name").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void StartupLineOfADemonstrationWarnsThatNoRealMaterialBelongsThere()
    {
        var line = ComposedConsole.LineWrittenBy(logger => logger.Declared(new Installation(InstallationKind.Demonstration)));

        Assert.Contains("warn", line, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Instalação de demonstração: roda sem cópia de guarda da chave e sem recuperação testada; não ingira material real", line, StringComparison.Ordinal);
    }

    [Fact]
    public void StartupLineOfADemonstrationCarriesTheMarkedName()
    {
        var line = ComposedConsole.LineWrittenBy(logger => logger.Declared(new Installation(InstallationKind.Demonstration)));

        Assert.Contains(Mark, line, StringComparison.Ordinal);
    }

    [Fact]
    public void StartupLineOfARealInstallationSaysSoAndDoesNotWarn()
    {
        var line = ComposedConsole.LineWrittenBy(logger => logger.Declared(new Installation(InstallationKind.Real)));

        Assert.Contains("Instalação real", line, StringComparison.Ordinal);
        Assert.DoesNotContain("demonstração", line, StringComparison.OrdinalIgnoreCase);
    }
}
