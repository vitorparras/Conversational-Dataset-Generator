using Core.Platform.Startup;

namespace Core.Host.Tests;

/// <summary>
/// The serving host does not start without the declaration of where the seal of the chain is kept, and names the key (issue #62, part B1, CA-1); a situation outside the three, a destination
/// the situation needs and does not have, and one the host recognises as under the control of the database are refused too (CA-2), and the migration mode, which seals nothing, does not ask for it.
/// </summary>
public sealed class SealDeclarationRefusalTests
{
    [Fact]
    public async Task HostWithoutTheSituationEndsWithTheRefusalCodeAndNamesTheKey()
    {
        var exit = await RunWithAsync();

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("'Custody:SealSituation'", exit.Error, StringComparison.Ordinal);
        Assert.Contains("não tem valor padrão", exit.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("zqx")]
    [InlineData("manual")]
    [InlineData("External;Manual")]
    public async Task SituationOutsideTheThreeIsRefusedNamingTheKeyAndTheValue(string declared)
    {
        var exit = await RunWithAsync($"--Custody:SealSituation={declared}");

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("'Custody:SealSituation'", exit.Error, StringComparison.Ordinal);
        Assert.Contains($"'{declared}'", exit.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("External")]
    [InlineData("Internal")]
    public async Task SituationThatSendsTheSealWithoutADestinationIsRefusedNamingTheKey(string situation)
    {
        var exit = await RunWithAsync($"--Custody:SealSituation={situation}");

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("'Custody:SealDestination'", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DestinationOnTheSameMachineIsRefusedSayingWhichRuleCaughtIt()
    {
        var exit = await RunWithAsync("--Custody:SealSituation=External", "--Custody:SealDestination=https://127.0.0.1/selos");

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("'Custody:SealDestination'", exit.Error, StringComparison.Ordinal);
        Assert.Contains("loopback", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADestinationOutsideTheMachineIsEnoughToStart()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var address = HostProcess.FreeAddress();
        var installation = HostInstallation.Built(address) with { DeclaresSeal = false };
        installation = installation with { Arguments = [.. installation.Arguments, "--Custody:SealSituation=Internal", "--Custody:SealDestination=https://zqx-seal-vault.example.test/selos"] };

        using var host = await HostProcess.StartAsync(installation, address, HostProcess.StartupTimeout, cancellationToken);
        using var response = await host.GetAsync("/status", cancellationToken);

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task SituationGivenInTheEnvironmentIsEnoughToStart()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var address = HostProcess.FreeAddress();
        var installation = HostInstallation.Built(address) with
        {
            DeclaresSeal = false,
            Environment = new Dictionary<string, string> { ["CDG_Custody__SealSituation"] = "Manual" },
        };

        using var host = await HostProcess.StartAsync(installation, address, HostProcess.StartupTimeout, cancellationToken);
        using var response = await host.GetAsync("/status", cancellationToken);

        Assert.True(response.IsSuccessStatusCode);
    }

    private static Task<HostExit> RunWithAsync(params string[] arguments)
    {
        var installation = HostInstallation.Built(HostProcess.FreeAddress()) with { DeclaresSeal = false };
        installation = installation with { Arguments = [.. installation.Arguments, .. arguments] };
        return HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, TestContext.Current.CancellationToken);
    }
}
