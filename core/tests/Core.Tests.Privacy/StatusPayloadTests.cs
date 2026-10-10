using System.Reflection;
using Core.Host.Endpoints;

namespace Core.Tests.Privacy;

/// <summary>Requirement 021: the state the process reports carries no content, only state and names.</summary>
public sealed class StatusPayloadTests
{
    [Fact]
    public void StatusResponseCarriesOnlyStateFlagsAndCheckResults()
    {
        Assert.Equal(["Alive", "Ready", "Checks"], PropertyNames(typeof(StatusResponse)));
        Assert.Equal(["Name", "Status", "Message"], PropertyNames(typeof(StatusCheckResponse)));
    }

    private static IEnumerable<string> PropertyNames(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 && p.Name != "EqualityContract")
            .Select(p => p.Name);
}
