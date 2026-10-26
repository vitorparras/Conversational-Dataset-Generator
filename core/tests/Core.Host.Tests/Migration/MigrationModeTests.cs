using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using Core.Host.Migration;
using Core.Persistence.Migrations;
using Core.Platform.Startup;

namespace Core.Host.Tests.Migration;

/// <summary>The migration mode is asked for by its word, needs the database section, and ends with a code for each way it can end.</summary>
public sealed class MigrationModeTests
{
    private const string Password = "ZQX-SENHA-7731";

    [Fact]
    public void EveryOutcomeHasItsOwnExitCodeAndOnlyAppliedIsSuccess()
    {
        var codes = Enum.GetValues<MigrationOutcome>().Select(MigrationMode.ExitOf).ToList();

        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.Equal(ExitCode.Success, MigrationMode.ExitOf(MigrationOutcome.Applied));
    }

    [Fact]
    public void WordOnlyAsTheFirstArgumentAsksForTheModeAndIsNotPassedToTheConfiguration()
    {
        string[] args = ["migrate", "--Database:Host=zqx"];

        Assert.True(MigrationMode.Requested(args));
        Assert.Equal(["--Database:Host=zqx"], MigrationMode.ConfigurationArguments(args));
        Assert.False(MigrationMode.Requested(["--Database:Host=zqx", "migrate"]));
    }

    [Fact]
    public async Task ModeWithoutTheDatabaseSectionIsRefusedNamingTheFourKeys()
    {
        var installation = Migrating(ReadOnlyDictionary<string, string>.Empty);

        var exit = await HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, TestContext.Current.CancellationToken);

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("'Database:Host'", exit.Error, StringComparison.Ordinal);
        Assert.Contains("'Database:Name'", exit.Error, StringComparison.Ordinal);
        Assert.Contains("'Database:Username'", exit.Error, StringComparison.Ordinal);
        Assert.Contains("'Database:Password'", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ModeAgainstAServerThatDoesNotAnswerEndsWithTheNotConnectedCodeAndNeverShowsThePassword()
    {
        var installation = Migrating(Database($"127.0.0.1:{FreePort()}"));

        var exit = await HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, TestContext.Current.CancellationToken);

        Assert.Equal((int)ExitCode.MigrationNotConnected, exit.Code);
        Assert.Contains("Migração não rodou", exit.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, exit.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, exit.Error, StringComparison.Ordinal);
    }

    private static HostInstallation Migrating(ReadOnlyDictionary<string, string> environment)
    {
        var installation = HostInstallation.Built(HostProcess.FreeAddress());
        return installation with { Arguments = ["migrate", .. installation.Arguments], Environment = environment };
    }

    private static ReadOnlyDictionary<string, string> Database(string host) =>
        new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CDG_Database__Host"] = host,
            ["CDG_Database__Name"] = "zqx_database",
            ["CDG_Database__Username"] = "zqx_owner",
            ["CDG_Database__Password"] = Password,
        });

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
