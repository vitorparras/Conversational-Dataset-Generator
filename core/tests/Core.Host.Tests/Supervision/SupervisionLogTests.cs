using Core.Host.Supervision;

namespace Core.Host.Tests.Supervision;

/// <summary>The supervision logs the loss of the service and each restart with what ties them together, and never the secret.</summary>
[Trait("Category", "RealService")]
public sealed class SupervisionLogTests : IDisposable
{
    private static readonly TimeSpan Margin = TimeSpan.FromSeconds(1);

    private readonly SupervisionHarness harness = new();
    private readonly ServiceProcessWatch watch = new();

    [Fact]
    public async Task LossOfTheServiceIsLoggedWithTheNumberOfTheProcessThatEnded()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var supervisor = await harness.StartedSupervisorAsync(watch, cancellationToken);
        var id = supervisor.ProcessId!.Value;

        StandInService.Kill(id);
        var lost = await Poll.UntilAsync(() => Task.FromResult(watch.State == ServiceState.Unavailable), Poll.LossLimit, cancellationToken);

        Assert.True(lost, "the end of the service process was not seen");
        Assert.Contains(harness.Log.Lines, line => line.StartsWith($"Serviço de linguagem encerrou (processo {id},", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RestartIsLoggedWhenAskedAndWhenEndedWithTheSameReference()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var request = new RestartRequest(RestartRequester.Operator, Guid.NewGuid());
        var supervisor = await harness.StartedSupervisorAsync(watch, cancellationToken);

        _ = await supervisor.RestartAsync(request, SupervisionHarness.Timeout, cancellationToken);

        Assert.Contains($"Reinício do serviço de linguagem pedido por Operator (referência {request.Reference})", harness.Log.Lines);
        Assert.Contains($"Reinício do serviço de linguagem terminou com o serviço Available (referência {request.Reference})", harness.Log.Lines);
    }

    [Fact]
    public async Task CanceledRestartIsLoggedAsEndedWithTheServiceUnavailable()
    {
        var request = new RestartRequest(RestartRequester.Operator, Guid.NewGuid());
        var supervisor = await harness.StartedSupervisorAsync(watch, TestContext.Current.CancellationToken);

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => supervisor.RestartAsync(request, SupervisionHarness.Timeout, new CancellationToken(canceled: true)));

        Assert.Contains($"Reinício do serviço de linguagem terminou com o serviço Unavailable (referência {request.Reference})", harness.Log.Lines);
    }

    [Fact]
    public async Task NormalStopIsNotLoggedAsALoss()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var supervisor = await harness.StartedSupervisorAsync(watch, cancellationToken);
        var id = supervisor.ProcessId!.Value;

        supervisor.Dispose();
        await Task.Delay(Margin, cancellationToken);

        Assert.False(ProcessTree.IsRunning(id));
        Assert.Equal(ServiceState.Stopped, watch.State);
        Assert.DoesNotContain(harness.Log.Lines, line => line.StartsWith("Serviço de linguagem encerrou", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NoLineWrittenAcrossALossAndARestartHoldsEitherSecret()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var supervisor = await harness.StartedSupervisorAsync(watch, cancellationToken);
        var first = supervisor.Secret;
        StandInService.Kill(supervisor.ProcessId!.Value);
        var lost = await Poll.UntilAsync(() => Task.FromResult(watch.State == ServiceState.Unavailable), Poll.LossLimit, cancellationToken);
        Assert.True(lost, "the end of the service process was not seen");

        _ = await supervisor.RestartAsync(new RestartRequest(RestartRequester.Operator, Guid.NewGuid()), SupervisionHarness.Timeout, cancellationToken);

        Assert.NotEmpty(harness.Log.Lines);
        Assert.DoesNotContain(harness.Log.Lines, line => line.Contains(first, StringComparison.Ordinal));
        Assert.DoesNotContain(harness.Log.Lines, line => line.Contains(supervisor.Secret, StringComparison.Ordinal));
    }

    public void Dispose() => harness.Dispose();
}
