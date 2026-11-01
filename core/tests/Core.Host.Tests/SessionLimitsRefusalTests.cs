using Core.Platform.Startup;

namespace Core.Host.Tests;

/// <summary>
/// The serving host does not start without the two limits of a session, and names the one that is missing (issue #70, CA-4); a limit declared that is not
/// a duration above zero is refused too. The limits are the same in the file, the environment and the arguments, since they all go through the closed configuration.
/// </summary>
public sealed class SessionLimitsRefusalTests
{
    private const string Idle = "--Session:IdleTimeout=00:30:00";
    private const string Absolute = "--Session:AbsoluteTimeout=08:00:00";

    [Fact]
    public async Task HostWithoutTheIdleLimitEndsWithTheRefusalCodeAndNamesOnlyThatOne()
    {
        var exit = await RunWithAsync(Absolute);

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("'Session:IdleTimeout'", exit.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("'Session:AbsoluteTimeout'", exit.Error, StringComparison.Ordinal);
        Assert.Contains("não tem valor padrão", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HostWithoutTheAbsoluteLimitEndsWithTheRefusalCodeAndNamesOnlyThatOne()
    {
        var exit = await RunWithAsync(Idle);

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("'Session:AbsoluteTimeout'", exit.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("'Session:IdleTimeout'", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HostWithNeitherLimitNamesBoth()
    {
        var exit = await RunWithAsync();

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("'Session:IdleTimeout'", exit.Error, StringComparison.Ordinal);
        Assert.Contains("'Session:AbsoluteTimeout'", exit.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("zqx")]
    [InlineData("0")]
    [InlineData("30")]
    [InlineData("00:00:00")]
    [InlineData("-00:30:00")]
    [InlineData("00:30")]
    [InlineData("30 minutos")]
    public async Task IdleLimitThatIsNotADurationAboveZeroIsRefusedNamingTheKeyAndTheValue(string declared)
    {
        var exit = await RunWithAsync(Absolute, $"--Session:IdleTimeout={declared}");

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("'Session:IdleTimeout'", exit.Error, StringComparison.Ordinal);
        Assert.Contains($"'{declared}'", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AbsoluteLimitThatIsNotADurationAboveZeroIsRefusedNamingTheKeyAndTheValue()
    {
        var exit = await RunWithAsync(Idle, "--Session:AbsoluteTimeout=00:00:00");

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("'Session:AbsoluteTimeout'", exit.Error, StringComparison.Ordinal);
        Assert.Contains("'00:00:00'", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LimitsGivenInTheEnvironmentAreEnoughToStart()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var address = HostProcess.FreeAddress();
        var installation = HostInstallation.Built(address) with
        {
            DeclaresSessionLimits = false,
            Environment = new Dictionary<string, string> { ["CDG_Session__IdleTimeout"] = "00:30:00", ["CDG_Session__AbsoluteTimeout"] = "1.00:00:00" },
        };

        using var host = await HostProcess.StartAsync(installation, address, HostProcess.StartupTimeout, cancellationToken);
        using var response = await host.GetAsync("/status", cancellationToken);

        Assert.True(response.IsSuccessStatusCode);
    }

    private static Task<HostExit> RunWithAsync(params string[] arguments)
    {
        var installation = HostInstallation.Built(HostProcess.FreeAddress()) with { DeclaresSessionLimits = false };
        installation = installation with { Arguments = [.. installation.Arguments, .. arguments] };
        return HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, TestContext.Current.CancellationToken);
    }
}
