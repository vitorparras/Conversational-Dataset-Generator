using System.Diagnostics;

namespace Core.Host.Tests;

/// <summary>The process helper never leaves a host behind: past its timeout, the host is killed and its port closed.</summary>
public sealed class HostProcessTests
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task WaitForExitPastTheTimeoutKillsTheHostAndClosesItsPort()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var address = HostProcess.FreeAddress();
        using var host = HostProcess.Launch(HostInstallation.Built(address), address);

        _ = await Assert.ThrowsAsync<TimeoutException>(() => host.WaitForExitAsync(ShortTimeout, cancellationToken));

        Assert.True(host.HasExited);
        _ = await Assert.ThrowsAsync<HttpRequestException>(() => host.GetAsync("/status", cancellationToken));
    }

    [Fact]
    public async Task StartThatNeverAnswersFailsWithinTheTimeoutAndLeavesNoHostBehind()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var listening = HostProcess.FreeAddress();
        var probed = HostProcess.FreeAddress();
        var started = Stopwatch.StartNew();

        _ = await Assert.ThrowsAsync<TimeoutException>(
            () => HostProcess.StartAsync(HostInstallation.Built(listening), probed, ShortTimeout, cancellationToken));

        Assert.InRange(started.Elapsed, TimeSpan.Zero, HostProcess.StartupTimeout);
        using var client = new HttpClient { BaseAddress = listening };
        _ = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetAsync(new Uri("/status", UriKind.Relative), cancellationToken));
    }
}
