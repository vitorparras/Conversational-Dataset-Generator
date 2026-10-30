using Core.Host.Supervision;

namespace Core.Host.Tests.Supervision;

/// <summary>Every start of the service gets a new secret of 32 random bytes, and the supervisor is what keeps the service alive.</summary>
[Trait("Category", "RealService")]
public sealed class ServiceSecretTests : IDisposable
{
    private readonly SupervisionHarness harness = new();

    [Fact]
    public async Task SecretIs32BytesWrittenAsHexAndTheServiceAcceptsIt()
    {
        var supervisor = harness.SupervisorFor(LanguageServiceInterpreter.Path);

        var outcome = await supervisor.StartAsync(SupervisionHarness.Timeout, TestContext.Current.CancellationToken);

        Assert.Equal(StartOutcome.Started, outcome);
        Assert.Matches("^[0-9A-F]{64}$", supervisor.Secret);
    }

    [Fact]
    public async Task TwoStartsGetDifferentSecrets()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var first = harness.SupervisorFor(LanguageServiceInterpreter.Path);
        var second = harness.SupervisorFor(LanguageServiceInterpreter.Path);

        _ = await first.StartAsync(SupervisionHarness.Timeout, cancellationToken);
        _ = await second.StartAsync(SupervisionHarness.Timeout, cancellationToken);

        Assert.NotEqual(first.Secret, second.Secret);
    }

    [Fact]
    public async Task DisposingTheSupervisorEndsTheServiceProcess()
    {
        var supervisor = harness.SupervisorFor(LanguageServiceInterpreter.Path);
        _ = await supervisor.StartAsync(SupervisionHarness.Timeout, TestContext.Current.CancellationToken);
        var id = supervisor.ProcessId!.Value;

        supervisor.Dispose();

        Assert.False(ProcessTree.IsRunning(id));
    }

    public void Dispose() => harness.Dispose();
}
