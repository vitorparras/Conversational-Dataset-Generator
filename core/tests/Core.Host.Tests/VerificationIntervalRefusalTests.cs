using Core.Platform.Startup;

namespace Core.Host.Tests;

/// <summary>
/// The serving host does not start without the interval of the verification of the chain, and names the key (issue #61, part 2, CA-1); an interval declared that is not a duration
/// above zero is refused too, and the migration mode, which verifies nothing, does not ask for it.
/// </summary>
public sealed class VerificationIntervalRefusalTests
{
    [Fact]
    public async Task HostWithoutTheIntervalEndsWithTheRefusalCodeAndNamesTheKey()
    {
        var exit = await RunWithAsync();

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("'Custody:VerificationInterval'", exit.Error, StringComparison.Ordinal);
        Assert.Contains("não tem valor padrão", exit.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("zqx")]
    [InlineData("0")]
    [InlineData("5")]
    [InlineData("00:00:00")]
    [InlineData("-00:05:00")]
    [InlineData("00:05")]
    [InlineData("5 minutos")]
    public async Task IntervalThatIsNotADurationAboveZeroIsRefusedNamingTheKeyAndTheValue(string declared)
    {
        var exit = await RunWithAsync($"--Custody:VerificationInterval={declared}");

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("'Custody:VerificationInterval'", exit.Error, StringComparison.Ordinal);
        Assert.Contains($"'{declared}'", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IntervalGivenInTheEnvironmentIsEnoughToStart()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var address = HostProcess.FreeAddress();
        var installation = HostInstallation.Built(address) with
        {
            DeclaresVerificationInterval = false,
            Environment = new Dictionary<string, string> { ["CDG_Custody__VerificationInterval"] = "1.00:00:00" },
        };

        using var host = await HostProcess.StartAsync(installation, address, HostProcess.StartupTimeout, cancellationToken);
        using var response = await host.GetAsync("/status", cancellationToken);

        Assert.True(response.IsSuccessStatusCode);
    }

    private static Task<HostExit> RunWithAsync(params string[] arguments)
    {
        var installation = HostInstallation.Built(HostProcess.FreeAddress()) with { DeclaresVerificationInterval = false };
        installation = installation with { Arguments = [.. installation.Arguments, .. arguments] };
        return HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, TestContext.Current.CancellationToken);
    }
}
