using Core.Host.Endpoints;
using Core.Platform.Startup;

namespace Core.Tests.Properties;

/// <summary>Issue 24: ready is exactly "every check passed", whatever the mix of results.</summary>
public sealed class StatusPropertyTests
{
    [Fact]
    public void ReadyHoldsExactlyWhenNoCheckFailed()
    {
        // Every combination of up to eight checks: bit i set means check i failed.
        for (var mask = 0; mask < 1 << 8; mask++)
        {
            for (var count = 0; count <= 8; count++)
            {
                var results = Enumerable.Range(0, count)
                    .Select(i => (mask & (1 << i)) != 0
                        ? CheckResult.Fail($"check-{i}", "ausente", "origem")
                        : CheckResult.Pass($"check-{i}", "presente", "origem"))
                    .ToList();

                var response = StatusEndpoint.Handle(new StartupReport(results));

                Assert.True(response.Alive);
                Assert.Equal(results.TrueForAll(r => r.Status == CheckStatus.Passed), response.Ready);
                Assert.Equal(count, response.Checks.Count);
            }
        }
    }
}
