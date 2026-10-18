using Core.Host.Supervision;

namespace Core.Host.Tests.Supervision;

/// <summary>The wait ends in one of three ways, and only an authenticated success counts as healthy.</summary>
public sealed class HealthWaitTests : IDisposable
{
    private static readonly TimeSpan ShortLimit = TimeSpan.FromSeconds(3);

    private readonly SupervisionHarness harness = new();

    [Fact]
    public async Task ServiceHoldingTheSameSecretIsHealthy()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var secret = SupervisionHarness.NewSecret();
        using var service = SupervisionHarness.Launcher.Start(secret)!;

        var outcome = await harness.Health.UntilHealthyAsync(service, secret, SupervisionHarness.Timeout, cancellationToken);

        Assert.Equal(StartOutcome.Started, outcome);
    }

    [Fact]
    public async Task ServiceThatRefusesTheSecretTimesOutInsteadOfCountingAsHealthy()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var secret = SupervisionHarness.NewSecret();
        using var service = SupervisionHarness.Launcher.Start(secret)!;

        var outcome = await harness.Health.UntilHealthyAsync(service, SupervisionHarness.NewSecret(), ShortLimit, cancellationToken);

        Assert.Equal(StartOutcome.TimedOut, outcome);
    }

    [Fact]
    public async Task ServiceThatEndsBeforeAnsweringReportsTheExit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var service = SupervisionHarness.Launcher.Start("short")!;

        var outcome = await harness.Health.UntilHealthyAsync(service, "short", SupervisionHarness.Timeout, cancellationToken);

        Assert.Equal(StartOutcome.ProcessExited, outcome);
    }

    public void Dispose() => harness.Dispose();
}
