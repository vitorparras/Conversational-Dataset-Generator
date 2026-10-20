using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Core.Platform.Startup;

namespace Core.Host.Tests;

/// <summary>
/// The host with a database declared checks the server before the language service starts and refuses to start when it
/// cannot; without one it says, in its status, that nothing was checked.
/// </summary>
public sealed class ServerGuaranteesHostTests
{
    private const string Password = "ZQX-SENHA-7731";

    [Fact]
    public async Task DeclaredDatabaseThatDoesNotAnswerEndsTheProcessWithTheCheckCodeNamingTheCheck()
    {
        var installation = HostInstallation.Built(HostProcess.FreeAddress()) with { Environment = Database($"127.0.0.1:{FreePort()}") };

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
    public async Task HostWithoutADatabaseIsReadyAndSaysTheDatabaseIsNotConfigured()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(cancellationToken);

        using var response = await host.GetAsync("/status", cancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        Assert.True(body.RootElement.GetProperty("ready").GetBoolean());
        var database = Assert.Single(
            body.RootElement.GetProperty("checks").EnumerateArray(), check => check.GetProperty("name").GetString() == "Database");
        Assert.Equal("NotConfigured", database.GetProperty("status").GetString());
        Assert.StartsWith("banco não configurado", database.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    private static ReadOnlyDictionary<string, string> Database(string host) =>
        new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CDG_Database__Host"] = host,
            ["CDG_Database__Name"] = "zqx_database",
            ["CDG_Database__Username"] = "zqx_app",
            ["CDG_Database__Password"] = Password,
        });

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
