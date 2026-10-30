using System.Net;
using Core.Host.Supervision;

namespace Core.Host.Tests.Supervision;

/// <summary>The supervisor moves the service through its states; a restart replaces the process with one that holds a new secret, and the secret of the previous one stops being accepted.</summary>
public sealed class ServiceLifecycleTests : IDisposable
{
    private static readonly RestartRequest Request = new(RestartRequester.Operator, Guid.NewGuid());

    private readonly SupervisionHarness harness = new();
    private readonly ServiceProcessWatch watch = new();

    [Trait("Category", "RealService")]
    [Fact]
    public async Task ServiceAfterRestartIsUnreachableWithTheSecretOfThePreviousProcess()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var supervisor = await harness.StartedSupervisorAsync(watch, cancellationToken);
        var previous = supervisor.Secret;

        _ = await supervisor.RestartAsync(Request, SupervisionHarness.Timeout, cancellationToken);
        var refused = await Assert.ThrowsAsync<HttpRequestException>(() => SupervisionHarness.HealthAsync(supervisor.Port!.Value, previous, cancellationToken));

        Assert.Equal(HttpRequestError.ConnectionError, refused.HttpRequestError);
    }

    [Trait("Category", "RealService")]
    [Fact]
    public async Task ServiceAfterRestartAcceptsItsNewSecret()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var supervisor = await harness.StartedSupervisorAsync(watch, cancellationToken);
        var previous = supervisor.Secret;

        var outcome = await supervisor.RestartAsync(Request, SupervisionHarness.Timeout, cancellationToken);
        var status = await SupervisionHarness.HealthAsync(supervisor.Port!.Value, supervisor.Secret, cancellationToken);

        Assert.Equal(StartOutcome.Started, outcome);
        Assert.NotEqual(previous, supervisor.Secret);
        Assert.Equal(HttpStatusCode.OK, status);
    }

    [Trait("Category", "RealService")]
    [Fact]
    public async Task RestartEndsThePreviousProcessAndStartsAnother()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var supervisor = await harness.StartedSupervisorAsync(watch, cancellationToken);
        var previous = supervisor.ProcessId!.Value;

        _ = await supervisor.RestartAsync(Request, SupervisionHarness.Timeout, cancellationToken);

        Assert.NotEqual(previous, supervisor.ProcessId);
        Assert.False(ProcessTree.IsRunning(previous));
        Assert.Equal(ServiceState.Available, watch.State);
    }

    [Trait("Category", "RealService")]
    [Fact]
    public async Task RestartMakesALostServiceAvailableAgain()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var supervisor = await harness.StartedSupervisorAsync(watch, cancellationToken);
        StandInService.Kill(supervisor.ProcessId!.Value);
        var lost = await Poll.UntilAsync(() => Task.FromResult(watch.State == ServiceState.Unavailable), Poll.LossLimit, cancellationToken);

        var outcome = await supervisor.RestartAsync(Request, SupervisionHarness.Timeout, cancellationToken);

        Assert.True(lost, "the end of the service process was not seen");
        Assert.Equal(StartOutcome.Started, outcome);
        Assert.Equal(ServiceState.Available, watch.State);
    }

    [Trait("Category", "RealService")]
    [Fact]
    public async Task CanceledRestartLeavesTheServiceUnavailable()
    {
        var supervisor = await harness.StartedSupervisorAsync(watch, TestContext.Current.CancellationToken);

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => supervisor.RestartAsync(Request, SupervisionHarness.Timeout, new CancellationToken(canceled: true)));

        Assert.Equal(ServiceState.Unavailable, watch.State);
        Assert.Null(supervisor.ProcessId);
    }

    [Trait("Category", "RealService")]
    [Fact]
    public async Task RestartAfterACanceledOneStartsTheService()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var supervisor = await harness.StartedSupervisorAsync(watch, cancellationToken);
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => supervisor.RestartAsync(Request, SupervisionHarness.Timeout, new CancellationToken(canceled: true)));

        var outcome = await supervisor.RestartAsync(Request, SupervisionHarness.Timeout, cancellationToken);

        Assert.Equal(StartOutcome.Started, outcome);
        Assert.Equal(ServiceState.Available, watch.State);
    }

    [Trait("Category", "RealService")]
    [Fact]
    public async Task RestartBeforeTheFirstStartIsADefect()
    {
        var supervisor = harness.SupervisorFor(LanguageServiceInterpreter.Path, watch);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            () => supervisor.RestartAsync(Request, SupervisionHarness.Timeout, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ServiceThatCannotStartIsUnavailable()
    {
        var supervisor = harness.SupervisorFor("zqx-missing-7731", watch);

        _ = await supervisor.StartAsync(SupervisionHarness.Timeout, TestContext.Current.CancellationToken);

        Assert.Equal(ServiceState.Unavailable, watch.State);
    }

    [Trait("Category", "RealService")]
    [Fact]
    public async Task DisposingTheSupervisorLeavesTheServiceStopped()
    {
        var supervisor = await harness.StartedSupervisorAsync(watch, TestContext.Current.CancellationToken);

        supervisor.Dispose();

        Assert.Equal(ServiceState.Stopped, watch.State);
    }

    public void Dispose() => harness.Dispose();
}
