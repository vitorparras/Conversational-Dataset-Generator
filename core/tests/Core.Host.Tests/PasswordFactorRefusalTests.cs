using Core.Platform.Startup;

namespace Core.Host.Tests;

/// <summary>
/// The serving host does not start without a work factor it can use (#69, CA-2): the process ends with the refusal code, and the refusal names Passwords:WorkFactor,
/// whether the key is missing, empty, not a number or outside the limits (a demonstration takes from 1 to 10000000; the floor of a real installation is in PasswordConfigurationTests).
/// </summary>
public sealed class PasswordFactorRefusalTests
{
    private const string Key = "'Passwords:WorkFactor'";

    [Fact]
    public async Task HostThatDeclaresNoFactorEndsWithTheRefusalCodeAndNamesTheKey()
    {
        var installation = HostInstallation.Built(HostProcess.FreeAddress()) with { DeclaresWorkFactor = false };

        var exit = await HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, TestContext.Current.CancellationToken);

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains(Key, exit.Error, StringComparison.Ordinal);
        Assert.Contains("não tem valor padrão", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HostThatDeclaresAnEmptyFactorEndsWithTheRefusalCodeAndNamesTheKey()
    {
        var exit = await RefusedWithAsync(string.Empty);

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains(Key, exit.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("zqx")]
    [InlineData("1.5")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("99999999999")]
    [InlineData("10000001")]
    public async Task HostThatDeclaresAFactorThatIsNotAWholeNumberInsideTheLimitsEndsWithTheRefusalCodeAndNamesTheKeyAndTheValue(string declared)
    {
        var exit = await RefusedWithAsync(declared);

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains(Key, exit.Error, StringComparison.Ordinal);
        Assert.Contains($"'{declared}'", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HostThatDeclaresAFactorInTheEnvironmentThatIsNotPositiveIsRefusedAsWell()
    {
        var installation = HostInstallation.Built(HostProcess.FreeAddress()) with
        {
            DeclaresWorkFactor = false,
            Environment = new Dictionary<string, string> { ["CDG_Passwords__WorkFactor"] = "0" },
        };

        var exit = await HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, TestContext.Current.CancellationToken);

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains(Key, exit.Error, StringComparison.Ordinal);
    }

    private static Task<HostExit> RefusedWithAsync(string declared)
    {
        var installation = HostInstallation.Built(HostProcess.FreeAddress()) with { DeclaresWorkFactor = false };
        installation = installation with { Arguments = [.. installation.Arguments, $"--Passwords:WorkFactor={declared}"] };
        return HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, TestContext.Current.CancellationToken);
    }
}
