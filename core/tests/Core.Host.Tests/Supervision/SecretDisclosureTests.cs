namespace Core.Host.Tests.Supervision;

/// <summary>The secret reaches the service through standard input and nowhere else another process on the machine could read it.</summary>
public sealed class SecretDisclosureTests : IDisposable
{
    private readonly SupervisionHarness harness = new();

    [Fact]
    public async Task CommandLinesOfTheServiceProcessesHoldNoSecret()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var secret = SupervisionHarness.NewSecret();
        var service = await harness.StartHealthyAsync(secret, cancellationToken);

        var lines = await ProcessTree.TreeOfAsync(service.ProcessId, cancellationToken);

        Assert.Contains(lines, line => line.CommandLine.Contains("language_service", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.CommandLine.Contains(secret, StringComparison.Ordinal));
    }

    [Fact]
    public async Task NoFileHoldsTheSecretOnceTheServiceIsUp()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var secret = SupervisionHarness.NewSecret();
        using var marker = new SweepMarker();
        _ = await harness.StartHealthyAsync(secret, cancellationToken);

        var files = SecretSweep.FilesHolding(secret, marker.Since);

        Assert.Empty(files);
    }

    public void Dispose() => harness.Dispose();
}
