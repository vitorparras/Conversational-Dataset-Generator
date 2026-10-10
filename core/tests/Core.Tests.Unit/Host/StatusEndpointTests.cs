using Core.Host.Endpoints;

namespace Core.Tests.Unit.Host;

public sealed class StatusEndpointTests
{
    [Fact]
    public void StatusReportsTheProcessAsAlive()
    {
        var response = StatusEndpoint.Handle();

        Assert.True(response.Alive);
    }
}
