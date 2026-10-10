using System.Reflection;
using Core.Host.Endpoints;

namespace Core.Tests.Privacy;

/// <summary>Requirement 021: the state the process reports carries no content.</summary>
public sealed class StatusPayloadTests
{
    [Fact]
    public void StatusResponseCarriesOnlyStateFlags()
    {
        var properties = typeof(StatusResponse)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => (p.Name, p.PropertyType));

        Assert.All(properties, p => Assert.Equal(typeof(bool), p.PropertyType));
    }
}
