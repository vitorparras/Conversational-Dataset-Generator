using System.Text.Json;
using Core.Host.Supervision;
using Core.Host.Tests.Supervision;
using Core.Platform.Startup;

namespace Core.Host.Tests;

/// <summary>
/// The status route keeps "alive" apart from "ready", shows the recognizer in force and the language service with its
/// state, and lists each startup check with its result; with the fake, readiness does not wait for a service it never asks.
/// </summary>
[Trait("Category", "RealService")]
public sealed class StatusReadinessTests : IDisposable
{
    private static readonly StartupReport PassedReport =
        new([CheckResult.Passed("Sample:File", "recurso presente", "ZQX-MARKER-7731.bin")], DateTimeOffset.UnixEpoch);

    private readonly RunningService service = StandInService.Start();
    private readonly ServiceProcessWatch watch = new();

    public StatusReadinessTests()
    {
        watch.Watch(service, _ => { });
    }

    [Fact]
    public async Task ReportWithAFailedCheckIsAnsweredAsAliveNotReadyAndNamesTheCheck()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var report = new StartupReport([CheckResult.Failed("Sample:File", "recurso ausente", "ZQX-MARKER-7731.bin")], DateTimeOffset.UnixEpoch);
        await using var host = await StatusHost.StartAsync(report, watch, cancellationToken);

        using var body = await host.StatusAsync(cancellationToken);

        Assert.True(body.RootElement.GetProperty("alive").GetBoolean());
        Assert.False(body.RootElement.GetProperty("ready").GetBoolean());
        var check = Assert.Single(body.RootElement.GetProperty("checks").EnumerateArray());
        Assert.Equal("Sample:File", check.GetProperty("name").GetString());
        Assert.Equal("Failed", check.GetProperty("status").GetString());
        Assert.Equal("ZQX-MARKER-7731.bin", check.GetProperty("expectedSource").GetString());
    }

    [Fact]
    public async Task PassedChecksWithTheServiceAvailableMakeTheProcessReady()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StatusHost.StartAsync(PassedReport, watch, cancellationToken);

        using var body = await host.StatusAsync(cancellationToken);

        Assert.True(body.RootElement.GetProperty("ready").GetBoolean());
        Assert.Equal("Available", body.RootElement.GetProperty("languageService").GetString());
        Assert.Equal("Passed", body.RootElement.GetProperty("checks")[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task ServiceProcessThatEndsTurnsTheProcessNotReadyAndShowsTheServiceUnavailable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StatusHost.StartAsync(PassedReport, watch, cancellationToken);

        StandInService.Kill(service.ProcessId);
        var lost = await Poll.UntilAsync(() => Task.FromResult(watch.State == ServiceState.Unavailable), Poll.LossLimit, cancellationToken);
        using var body = await host.StatusAsync(cancellationToken);

        Assert.True(lost, "the end of the service process was not seen");
        Assert.False(body.RootElement.GetProperty("ready").GetBoolean());
        Assert.Equal("Unavailable", body.RootElement.GetProperty("languageService").GetString());
    }

    [Fact]
    public async Task StatusShowsWhatEachCheckObserved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var observed = CheckResult.Passed("Sample:File", "recurso presente", "Sample:Host") with { Observed = "ZQX-MARKER-7731 1.0" };
        await using var host = await StatusHost.StartAsync(new StartupReport([observed], DateTimeOffset.UnixEpoch), watch, cancellationToken);

        using var body = await host.StatusAsync(cancellationToken);

        Assert.Equal("ZQX-MARKER-7731 1.0", body.RootElement.GetProperty("checks")[0].GetProperty("observed").GetString());
    }

    [Fact]
    public async Task StatusSaysWhenTheChecksEnded()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var checkedAt = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
        var report = new StartupReport([CheckResult.Passed("Sample:File", "recurso presente", "ZQX-MARKER-7731.bin")], checkedAt);
        await using var host = await StatusHost.StartAsync(report, watch, cancellationToken);

        using var body = await host.StatusAsync(cancellationToken);

        Assert.Equal(checkedAt, body.RootElement.GetProperty("checkedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task FakeInForceIsReadyAlthoughNoStartEverMovesTheWatch()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StatusHost.StartAsync(PassedReport, new ServiceProcessWatch(), RecognizerKind.Fake, cancellationToken);

        using var body = await host.StatusAsync(cancellationToken);

        Assert.True(body.RootElement.GetProperty("ready").GetBoolean());
        Assert.Equal("fake", body.RootElement.GetProperty("recognizer").GetString());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("languageService").ValueKind);
    }

    [Fact]
    public async Task FakeInForceWithAFailedCheckIsNotReady()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var report = new StartupReport([CheckResult.Failed("Sample:File", "recurso ausente", "ZQX-MARKER-7731.bin")], DateTimeOffset.UnixEpoch);
        await using var host = await StatusHost.StartAsync(report, watch, RecognizerKind.Fake, cancellationToken);

        using var body = await host.StatusAsync(cancellationToken);

        Assert.False(body.RootElement.GetProperty("ready").GetBoolean());
    }

    [Fact]
    public async Task HttpInForceWithTheServiceNotYetAnsweringIsNotReady()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StatusHost.StartAsync(PassedReport, new ServiceProcessWatch(), RecognizerKind.Http, cancellationToken);

        using var body = await host.StatusAsync(cancellationToken);

        Assert.False(body.RootElement.GetProperty("ready").GetBoolean());
        Assert.Equal("http", body.RootElement.GetProperty("recognizer").GetString());
        Assert.Equal("Starting", body.RootElement.GetProperty("languageService").GetString());
    }

    public void Dispose() => service.Dispose();
}
