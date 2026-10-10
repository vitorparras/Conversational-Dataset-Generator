using Core.Host.Endpoints;
using Core.Platform.Startup;

namespace Core.Tests.Unit.Host;

/// <summary>Issue 24 CA-2 and issue 26 CA-3: alive and ready are separate, and every check is named.</summary>
public sealed class StatusEndpointTests
{
    [Fact]
    public void AllChecksPassedMeansAliveAndReadyWithEveryCheckNamed()
    {
        var report = new StartupReport(
        [
            CheckResult.Pass("first", "presente", "origem-a"),
            CheckResult.Pass("second", "presente", "origem-b"),
        ]);

        var response = StatusEndpoint.Handle(report);

        Assert.True(response.Alive);
        Assert.True(response.Ready);
        Assert.Equal(
            [new StatusCheckResponse("first", "passed", "presente"), new StatusCheckResponse("second", "passed", "presente")],
            response.Checks);
    }

    [Fact]
    public void FailingCheckMeansAliveButNotReadyAndNamesTheCheck()
    {
        var report = new StartupReport(
        [
            CheckResult.Pass("first", "presente", "origem-a"),
            CheckResult.Fail("second", "recurso ausente", "origem-b"),
        ]);

        var response = StatusEndpoint.Handle(report);

        Assert.True(response.Alive);
        Assert.False(response.Ready);
        Assert.Contains(new StatusCheckResponse("second", "failed", "recurso ausente"), response.Checks);
    }
}
