using System.Collections.ObjectModel;
using Core.Host.Migration;
using Core.Persistence.Migrations;
using Core.Platform.Startup;
using Core.Tests.Support.Net;

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
        using var closed = new ClosedPort();
        var installation = Migrating(Database($"127.0.0.1:{closed.Number}"));

        var exit = await HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, TestContext.Current.CancellationToken);

        Assert.Equal((int)ExitCode.MigrationNotConnected, exit.Code);
        Assert.Contains("Migração não rodou", exit.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, exit.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ModeDoesNotNeedTheKindOfInstallationThatTheServingModeCannotStartWithout()
    {
        using var closed = new ClosedPort();
        var installation = Migrating(Database($"127.0.0.1:{closed.Number}")) with { DeclaresInstallation = false, DeclaresWorkFactor = false };

        var exit = await HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, TestContext.Current.CancellationToken);

        Assert.Equal((int)ExitCode.MigrationNotConnected, exit.Code);
        Assert.DoesNotContain("Installation:Kind", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ModeDoesNotNeedTheCustodyFilesThatTheServingModeCannotStartWithout()
    {
        using var closed = new ClosedPort();
        var installation = Migrating(Database($"127.0.0.1:{closed.Number}")) with { DeclaresInstallation = false, DeclaresWorkFactor = false };

        var exit = await HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, TestContext.Current.CancellationToken);

        Assert.Equal((int)ExitCode.MigrationNotConnected, exit.Code);
        Assert.DoesNotContain("MasterKey", exit.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("Custody", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ModeRefusesAKindOfInstallationOutsideTheTwoItAcceptsNamingThatKeyAndNeverTheDatabaseOnes()
    {
        var installation = Migrating(Database("127.0.0.1:1")) with { DeclaresInstallation = false, Arguments = ["migrate", "--Installation:Kind=Real"] };

        var exit = await HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, TestContext.Current.CancellationToken);

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("'Installation:Kind'", exit.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("Database:", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ModeDoesNotNeedTheWorkFactorThatTheServingModeCannotStartWithout()
    {
        using var closed = new ClosedPort();
        var installation = Migrating(Database($"127.0.0.1:{closed.Number}")) with { DeclaresWorkFactor = false };

        var exit = await HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, TestContext.Current.CancellationToken);

        Assert.Equal((int)ExitCode.MigrationNotConnected, exit.Code);
        Assert.DoesNotContain("Passwords:WorkFactor", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ModeRefusesAWorkFactorGivenThatIsNotAWholeNumberAboveZeroNamingThatKey()
    {
        var installation = Migrating(Database("127.0.0.1:1")) with { DeclaresWorkFactor = false, Arguments = ["migrate", "--Passwords:WorkFactor=zqx"] };

        var exit = await HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, TestContext.Current.CancellationToken);

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("Passwords:WorkFactor", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ModeHoldsAWorkFactorGivenWithoutAKindOfInstallationToTheFloorOfARealOneNamingTheKeyAndTheMinimum()
    {
        var installation = Migrating(Database("127.0.0.1:1")) with { DeclaresInstallation = false, DeclaresWorkFactor = false, Arguments = ["migrate", "--Passwords:WorkFactor=599999"] };

        var exit = await HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, TestContext.Current.CancellationToken);

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("Passwords:WorkFactor", exit.Error, StringComparison.Ordinal);
        Assert.Contains("600000", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ModeRefusedForAnotherKeyAlsoNamesTheFourDatabaseKeysThatAreMissing()
    {
        var installation = Migrating(ReadOnlyDictionary<string, string>.Empty) with { DeclaresInstallation = false, Arguments = ["migrate", "--Installation:Kind=Real"] };

        var exit = await HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, TestContext.Current.CancellationToken);

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("'Installation:Kind'", exit.Error, StringComparison.Ordinal);
        Assert.Contains("'Database:Host'", exit.Error, StringComparison.Ordinal);
        Assert.Contains("'Database:Name'", exit.Error, StringComparison.Ordinal);
        Assert.Contains("'Database:Username'", exit.Error, StringComparison.Ordinal);
        Assert.Contains("'Database:Password'", exit.Error, StringComparison.Ordinal);
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
}
