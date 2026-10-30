using Core.Host.Tests.Supervision;
using Core.Platform.Startup;

namespace Core.Host.Tests;

/// <summary>SIGTERM or CTRL+C ends the host as asked: no language service is left, its stop line reaches the log file, and the exit code is success.</summary>
public sealed class ShutdownTests
{
    private const string LogFileName = "ZQX-LOG-7731.log";
    private const string StopLine = "INFO  Core.Host.Supervision.LanguageServiceSupervisor[110] Serviço de linguagem encerrado (processo ";

    /// <summary>The service gets five seconds to leave before it is killed, so the whole stop fits well inside this.</summary>
    private static readonly TimeSpan StopLimit = HostProcess.StartupTimeout;

    [LinuxFact]
    public async Task TerminationSignalEndsTheHostWithSuccess()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(cancellationToken);

        host.Terminate();
        var exit = await host.WaitForExitAsync(StopLimit, cancellationToken);

        Assert.Equal((int)ExitCode.Success, exit.Code);
    }

    [LinuxFact]
    public async Task InterruptSignalThatCtrlCSendsEndsTheHostWithSuccess()
    {
        Assert.SkipWhen(LinuxOnly.InterruptIgnored, LinuxOnly.InterruptIgnoredReason);
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await HostProcess.StartAsync(cancellationToken);

        host.Signal("INT");
        var exit = await host.WaitForExitAsync(StopLimit, cancellationToken);

        Assert.Equal((int)ExitCode.Success, exit.Code);
    }

    /// <summary>Nothing of the service is left once the host is gone; the order of the two ends is proved on the supervisor alone.</summary>
    [Trait("Category", "RealService")]
    [LinuxFact]
    public async Task TerminationSignalLeavesNoLanguageServiceRunning()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var host = await StartWithRealServiceAsync(cancellationToken);
        var tree = await ProcessTree.TreeOfAsync(host.Id, cancellationToken);
        var service = tree.Where(line => line.Id != host.Id).Select(line => ProcessIdentity.Of(line.Id)).OfType<ProcessIdentity>().ToList();
        using var survivors = new Survivors(service);

        host.Terminate();
        _ = await host.WaitForExitAsync(StopLimit, cancellationToken);

        Assert.NotEmpty(service);
        Assert.DoesNotContain(service, ProcessTree.IsRunning);
    }

    [Trait("Category", "RealService")]
    [LinuxFact]
    public async Task LineOfTheLanguageServiceStopReachesTheLogFileBeforeItCloses()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, LogFileName);
        using var host = await StartWithLogFileAsync(path, cancellationToken);

        host.Terminate();
        _ = await host.WaitForExitAsync(StopLimit, cancellationToken);

        var file = await File.ReadAllTextAsync(path, cancellationToken);
        Assert.Contains(LogLines.Untimed(file), line => line.StartsWith(StopLine, StringComparison.Ordinal));
    }

    /// <summary>A host that starts the real language service, the one whose end these tests observe.</summary>
    private static Task<HostProcess> StartWithRealServiceAsync(CancellationToken cancellationToken)
    {
        var address = HostProcess.FreeAddress();
        return HostProcess.StartAsync(RealService(address), address, HostProcess.StartupTimeout, cancellationToken);
    }

    private static Task<HostProcess> StartWithLogFileAsync(string path, CancellationToken cancellationToken)
    {
        var address = HostProcess.FreeAddress();
        return HostProcess.StartAsync(RealService(address).WithLogFile(path), address, HostProcess.StartupTimeout, cancellationToken);
    }

    private static HostInstallation RealService(Uri address) => HostInstallation.Built(address) with { UsesRealService = true };
}
