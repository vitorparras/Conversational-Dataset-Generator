using System.Collections.ObjectModel;
using Core.Platform.Startup;
using Core.Tests.Support.Net;

namespace Core.Host.Tests;

/// <summary>
/// The host with a database declared checks the server before the language service starts and refuses to start when it
/// cannot; without one it does not start at all.
/// </summary>
public sealed class ServerGuaranteesHostTests
{
    private const string Password = "ZQX-SENHA-7731";

    [Fact]
    public async Task DeclaredDatabaseThatDoesNotAnswerEndsTheProcessWithTheCheckCodeNamingTheCheck()
    {
        using var closed = new ClosedPort();
        var installation = HostInstallation.Built(HostProcess.FreeAddress()) with { Environment = Database($"127.0.0.1:{closed.Number}") };

        var exit = await HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, TestContext.Current.CancellationToken);

        Assert.Equal((int)ExitCode.StartupCheckFailed, exit.Code);
        Assert.Contains("  - Privilege: o servidor de banco não respondeu", exit.Error, StringComparison.Ordinal);
        Assert.Contains("(origem esperada: Database:Host; observado: nada)", exit.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PortThatIsNotANumberRefusesTheConfigurationNamingTheHostKey()
    {
        var installation = HostInstallation.Built(HostProcess.FreeAddress()) with { Environment = Database("127.0.0.1:zqx") };

        var exit = await HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, TestContext.Current.CancellationToken);

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("valor não aceito para 'Database:Host': encontrado '127.0.0.1:zqx'", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListOfServersRefusesTheConfigurationInsteadOfFailingTheProcess()
    {
        var installation = HostInstallation.Built(HostProcess.FreeAddress()) with { Environment = Database("127.0.0.1:zqx,127.0.0.1:1") };

        var exit = await HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, TestContext.Current.CancellationToken);

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("valor não aceito para 'Database:Host'", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HostWithoutAnyDatabaseKeyEndsTheProcessWithTheRefusalCodeNamingTheFourKeys()
    {
        var installation = HostInstallation.Built(HostProcess.FreeAddress()) with { ProvidesDatabase = false };

        var exit = await HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, TestContext.Current.CancellationToken);

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("chave exigida ausente 'Database:Host'", exit.Error, StringComparison.Ordinal);
        Assert.Contains("chave exigida ausente 'Database:Name'", exit.Error, StringComparison.Ordinal);
        Assert.Contains("chave exigida ausente 'Database:Username'", exit.Error, StringComparison.Ordinal);
        Assert.Contains("chave exigida ausente 'Database:Password'", exit.Error, StringComparison.Ordinal);
    }

    private static ReadOnlyDictionary<string, string> Database(string host) =>
        new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CDG_Database__Host"] = host,
            ["CDG_Database__Name"] = "zqx_database",
            ["CDG_Database__Username"] = "zqx_app",
            ["CDG_Database__Password"] = Password,
        });
}
