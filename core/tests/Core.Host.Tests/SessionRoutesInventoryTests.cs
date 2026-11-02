using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Core.Host.Tests;

/// <summary>
/// Every route the host maps asks for a session except the explicit exceptions (issue #495, CA-3): the inventory is read from the routes the composition
/// maps, so a route added later that answers with no session, and is not on the list, fails here. What the inventory cannot see, a branch of the pipeline
/// or a terminal middleware and who may mark a route open, is judged by the architecture rule.
/// </summary>
public sealed class SessionRoutesInventoryTests
{
    /// <summary>The only routes that answer with no session: the entry, which makes one, and the status.</summary>
    private static readonly string[] Exceptions = ["POST /session", "GET /status"];

    [Fact]
    public void OnlyTheEntryAndTheStatusAnswerWithNoSession()
    {
        var open = MappedRoutes.Read().Where(route => route.Open).Select(route => route.ToString());

        Assert.Equal(Exceptions.Order(StringComparer.Ordinal), open.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void EveryOtherRouteTheHostMapsIsBehindTheGate()
    {
        var gated = MappedRoutes.Read().Where(route => !route.Open).Select(route => route.ToString()).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(["DELETE /session", "GET /configuration", "GET /session", "GET /versions", "POST /analysis"], gated);
    }

    [Fact]
    public void NoRouteIsShortCircuitedPastTheGate()
    {
        var skipping = MappedRoutes.Read().Where(route => route.ShortCircuited).Select(route => route.ToString());

        Assert.Empty(skipping);
    }

    [Fact]
    public void ShortCircuitedRouteIsSeenByTheInventory()
    {
        var seen = MappedRoutes.Read(app => _ = app.MapGet("/zqx-skipping-7731", () => Results.NoContent()).ShortCircuit());

        var skipping = Assert.Single(seen, route => route.ShortCircuited);
        Assert.Equal("GET /zqx-skipping-7731", skipping.ToString());
    }
}
