using Core.Host.Endpoints;

namespace Core.Tests.Properties;

/// <summary>Issue 24: the process answers its own state the same way every time.</summary>
public sealed class StatusPropertyTests
{
    [Fact]
    public void StatusIsTheSameOnEveryCall()
    {
        var first = StatusEndpoint.Handle();

        for (var call = 0; call < 1000; call++)
        {
            Assert.Equal(first, StatusEndpoint.Handle());
        }
    }
}
