using Core.Platform.Startup;

namespace Core.Host.Tests;

/// <summary>
/// The serving host does not start without the conditions of origin, and names the key (#85); a list declared that repeats a name or holds what is not a name is refused saying what
/// was found and what is accepted, and the migration mode, which takes no material in, does not ask for it.
/// </summary>
public sealed class IngestionConditionsRefusalTests
{
    [Fact]
    public async Task HostWithoutTheListEndsWithTheRefusalCodeAndNamesTheKey()
    {
        var exit = await RunWithAsync();

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("'Ingestion:Conditions'", exit.Error, StringComparison.Ordinal);
        Assert.Contains("não tem valor padrão", exit.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("restrita,restrita")]
    [InlineData("restrita,,permissiva")]
    [InlineData("Restrita")]
    public async Task ListThatIsNotOneTheInstallationCanRunWithIsRefusedNamingTheKeyTheValueAndWhatIsAccepted(string declared)
    {
        var exit = await RunWithAsync($"--Ingestion:Conditions={declared}");

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("'Ingestion:Conditions'", exit.Error, StringComparison.Ordinal);
        Assert.Contains($"'{declared}'", exit.Error, StringComparison.Ordinal);
        Assert.Contains("da condição mais restrita para a mais permissiva", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListGivenInTheEnvironmentIsEnoughToStart()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var address = HostProcess.FreeAddress();
        var installation = HostInstallation.Built(address) with
        {
            DeclaresIngestionConditions = false,
            Environment = new Dictionary<string, string> { ["CDG_Ingestion__Conditions"] = "restrita,permissiva" },
        };

        using var host = await HostProcess.StartAsync(installation, address, HostProcess.StartupTimeout, cancellationToken);
        using var response = await host.GetAsync("/status", cancellationToken);

        Assert.True(response.IsSuccessStatusCode);
    }

    private static Task<HostExit> RunWithAsync(params string[] arguments)
    {
        var installation = HostInstallation.Built(HostProcess.FreeAddress()) with { DeclaresIngestionConditions = false };
        installation = installation with { Arguments = [.. installation.Arguments, .. arguments] };
        return HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, TestContext.Current.CancellationToken);
    }
}
