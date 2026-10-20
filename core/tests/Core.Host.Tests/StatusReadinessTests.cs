using Core.Platform.Startup;

namespace Core.Host.Tests;

/// <summary>The status route keeps "alive" apart from "ready" and lists each startup check with its result.</summary>
public sealed class StatusReadinessTests
{
    [Fact]
    public async Task ReportWithAFailedCheckIsAnsweredAsAliveNotReadyAndNamesTheCheck()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var report = new StartupReport([CheckResult.Failed("Sample:File", "recurso ausente", "ZQX-MARKER-7731.bin")], DateTimeOffset.UnixEpoch);
        await using var host = await StatusHost.StartAsync(report, cancellationToken);

        using var body = await host.StatusAsync(cancellationToken);

        Assert.True(body.RootElement.GetProperty("alive").GetBoolean());
        Assert.False(body.RootElement.GetProperty("ready").GetBoolean());
        var check = Assert.Single(body.RootElement.GetProperty("checks").EnumerateArray());
        Assert.Equal("Sample:File", check.GetProperty("name").GetString());
        Assert.Equal("Failed", check.GetProperty("status").GetString());
        Assert.Equal("ZQX-MARKER-7731.bin", check.GetProperty("expectedSource").GetString());
    }

    [Fact]
    public async Task PassedChecksMakeTheProcessReady()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var report = new StartupReport([CheckResult.Passed("Sample:File", "recurso presente", "ZQX-MARKER-7731.bin")], DateTimeOffset.UnixEpoch);
        await using var host = await StatusHost.StartAsync(report, cancellationToken);

        using var body = await host.StatusAsync(cancellationToken);

        Assert.True(body.RootElement.GetProperty("ready").GetBoolean());
        Assert.Equal("Passed", body.RootElement.GetProperty("checks")[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task StatusSaysWhenTheChecksEnded()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var checkedAt = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
        var report = new StartupReport([CheckResult.Passed("Sample:File", "recurso presente", "ZQX-MARKER-7731.bin")], checkedAt);
        await using var host = await StatusHost.StartAsync(report, cancellationToken);

        using var body = await host.StatusAsync(cancellationToken);

        Assert.Equal(checkedAt, body.RootElement.GetProperty("checkedAt").GetDateTimeOffset());
    }
}
