namespace Core.Host.Tests.Supervision;

/// <summary>Disposing the supervisor ends the service before it returns, and writes the stop line only once the service has ended.</summary>
[Trait("Category", "RealService")]
public sealed class SupervisorStopTests : IDisposable
{
    private readonly SupervisionHarness harness = new();

    [Fact]
    public async Task DisposeEndsTheServiceBeforeItReturns()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var supervisor = harness.SupervisorFor(LanguageServiceInterpreter.Path);
        _ = await supervisor.StartAsync(SupervisionHarness.Timeout, cancellationToken);
        var service = supervisor.ProcessId!.Value;

        supervisor.Dispose();

        Assert.False(ProcessTree.IsRunning(service));
    }

    [Fact]
    public async Task StopLineIsWrittenOnlyOnceTheServiceHasEnded()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var witness = new StopLineWitness();
        var supervisor = harness.SupervisorFor(LanguageServiceInterpreter.Path, witness);
        _ = await supervisor.StartAsync(SupervisionHarness.Timeout, cancellationToken);

        supervisor.Dispose();

        Assert.False(witness.ServiceRunningAtStopLine);
    }

    public void Dispose() => harness.Dispose();
}
