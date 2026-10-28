using Core.Host.Supervision;

namespace Core.Host.Tests.Supervision;

/// <summary>The watch sees the end of the service process as it happens, and only the process it still watches counts as a loss.</summary>
public sealed class ServiceProcessWatchTests : IDisposable
{
    private static readonly TimeSpan Margin = TimeSpan.FromSeconds(1);

    private readonly RunningService service = StandInService.Start();
    private readonly ServiceProcessWatch watch = new();
    private readonly TaskCompletionSource<RunningService> lost = new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public void WatchedServiceIsAvailable()
    {
        watch.Watch(service, ended => lost.TrySetResult(ended));

        Assert.Equal(ServiceState.Available, watch.State);
    }

    [Fact]
    public async Task EndOfTheWatchedProcessMakesTheServiceUnavailableAndNamesTheProcess()
    {
        watch.Watch(service, ended => lost.TrySetResult(ended));

        StandInService.Kill(service.ProcessId);
        var ended = await lost.Task.WaitAsync(Poll.LossLimit, TestContext.Current.CancellationToken);

        Assert.Same(service, ended);
        Assert.Equal(ServiceState.Unavailable, watch.State);
    }

    [Fact]
    public async Task ReportOfTheLossThatThrowsStillMakesTheServiceUnavailable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        watch.Watch(service, _ => throw new InvalidOperationException("The report failed."));

        StandInService.Kill(service.ProcessId);
        var unavailable = await Poll.UntilAsync(() => Task.FromResult(watch.State == ServiceState.Unavailable), Poll.LossLimit, cancellationToken);

        Assert.True(unavailable, "a report that threw left a dead process reading as available");
    }

    [Fact]
    public async Task EndOfAProcessNoLongerWatchedIsNotALoss()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        watch.Watch(service, ended => lost.TrySetResult(ended));
        watch.Move(ServiceState.Restarting);

        StandInService.Kill(service.ProcessId);
        await service.WaitForExitAsync().WaitAsync(Poll.LossLimit, cancellationToken);
        await Task.Delay(Margin, cancellationToken);

        Assert.False(lost.Task.IsCompleted);
        Assert.Equal(ServiceState.Restarting, watch.State);
    }

    [Fact]
    public async Task EndOfTheProcessAfterTheStopIsNotALoss()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        watch.Watch(service, ended => lost.TrySetResult(ended));
        watch.Move(ServiceState.Stopped);

        StandInService.Kill(service.ProcessId);
        await service.WaitForExitAsync().WaitAsync(Poll.LossLimit, cancellationToken);
        await Task.Delay(Margin, cancellationToken);

        Assert.False(lost.Task.IsCompleted);
        Assert.Equal(ServiceState.Stopped, watch.State);
    }

    [Fact]
    public void MovingToAvailableWithoutAWatchedProcessIsRefused() =>
        _ = Assert.Throws<InvalidOperationException>(() => watch.Move(ServiceState.Available));

    [Fact]
    public void MoveTheSupervisionNeverMakesIsRefusedByTheWatch() =>
        _ = Assert.Throws<InvalidOperationException>(() => watch.Move(ServiceState.Restarting));

    public void Dispose() => service.Dispose();
}
