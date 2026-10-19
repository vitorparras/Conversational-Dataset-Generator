using Core.Platform.Startup;

namespace Core.Host.Tests;

/// <summary>A log file that opens and then refuses every write neither stops the host nor its console, and the host says so once.</summary>
public sealed class RefusedLogFileTests
{
    /// <summary>The device that opens for writing and refuses every write with "no space left on device", as a full disk does.</summary>
    private const string FullDevice = "/dev/full";
    private const string Notice = "O registro em arquivo parou: a escrita no arquivo falhou (disco cheio ou arquivo indisponível). O registro segue só no console.";

    [LinuxFact]
    public async Task HostWhoseLogFileRefusesWritesKeepsServingAndLoggingOnTheConsole()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await StartAsync(cancellationToken);

        using var response = await host.GetAsync("/status", cancellationToken);
        var output = await host.StopAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains(LogLines.Untimed(output), line => line.StartsWith("INFO  Microsoft.Hosting.Lifetime[14] Now listening on: ", StringComparison.Ordinal));
    }

    [LinuxFact]
    public async Task HostWhoseLogFileRefusesWritesEndsWithSuccessWhenTerminated()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await StartAsync(cancellationToken);

        host.Terminate();
        var exit = await host.WaitForExitAsync(HostProcess.StartupTimeout, cancellationToken);

        Assert.Equal((int)ExitCode.Success, exit.Code);
    }

    [LinuxFact]
    public async Task HostWhoseLogFileRefusesWritesSaysSoOnceOnTheErrorStreamWithoutThePath()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await StartAsync(cancellationToken);

        host.Terminate();
        var exit = await host.WaitForExitAsync(HostProcess.StartupTimeout, cancellationToken);

        _ = Assert.Single(exit.Error.Split('\n'), line => line == Notice);
        Assert.DoesNotContain(FullDevice, exit.Error, StringComparison.Ordinal);
    }

    private static Task<HostProcess> StartAsync(CancellationToken cancellationToken)
    {
        var address = HostProcess.FreeAddress();
        return HostProcess.StartAsync(
            HostInstallation.Built(address).WithLogFile(FullDevice), address, HostProcess.StartupTimeout, cancellationToken);
    }
}
