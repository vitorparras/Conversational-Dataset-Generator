namespace Core.Platform.Tests.Logging;

/// <summary>The process helper never leaves a process behind: past its limit, the process is killed before the failure propagates.</summary>
public sealed class ExternalProcessTests
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task WaitPastTheTimeoutKillsTheProcessAndFails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var process = ExternalProcess.Start(Sleeper.StartInfo());

        _ = await Assert.ThrowsAsync<TimeoutException>(() => process.WaitForExitAsync(ShortTimeout, cancellationToken));

        Assert.True(process.HasExited);
    }

    [Fact]
    public async Task RunPastTheTimeoutFailsWithoutWaitingForTheCommand()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var started = System.Diagnostics.Stopwatch.StartNew();

        _ = await Assert.ThrowsAsync<TimeoutException>(() => ExternalProcess.RunAsync(Sleeper.StartInfo(), ShortTimeout, cancellationToken));

        Assert.InRange(started.Elapsed, ShortTimeout, TimeSpan.FromSeconds(30));
    }
}
