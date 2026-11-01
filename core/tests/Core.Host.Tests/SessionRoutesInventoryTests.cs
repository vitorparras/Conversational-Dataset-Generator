using Core.Host.Endpoints;
using Core.Platform.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Core.Host.Tests;

/// <summary>
/// Every route the host maps asks for a session except the explicit exceptions (issue #495, CA-3): the inventory is read from the routes the composition
/// maps, so a route added later that answers with no session, and is not on the list, fails here.
/// </summary>
public sealed class SessionRoutesInventoryTests
{
    /// <summary>The only routes that answer with no session: the entry, which makes one, and the status.</summary>
    private static readonly string[] Exceptions = ["POST /session", "GET /status"];

    [Fact]
    public async Task OnlyTheEntryAndTheStatusAnswerWithNoSession()
    {
        await using var app = Mapped();

        var open = Routes(app).Where(route => route.Metadata.GetMetadata<SessionOpen>() is not null).Select(Described);

        Assert.Equal(Exceptions.Order(StringComparer.Ordinal), open.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task EveryOtherRouteTheHostMapsIsBehindTheGate()
    {
        await using var app = Mapped();

        var gated = Routes(app).Where(route => route.Metadata.GetMetadata<SessionOpen>() is null).Select(Described).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(["DELETE /session", "GET /configuration", "GET /session", "GET /versions", "POST /analysis"], gated);
    }

    private static string Described(RouteEndpoint route) =>
        $"{route.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single()} {route.RoutePattern.RawText}";

    private static IEnumerable<RouteEndpoint> Routes(WebApplication app) =>
        ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>();

    private static WebApplication Mapped()
    {
        var builder = CompositionRoot.ClosedBuilder(HostProcess.FreePlainAddress().GetLeftPart(UriPartial.Authority));
        CompositionRoot.Compose(builder, ClosedConfiguration.Load(new ConfigurationSchema([HostConfiguration.Section])).View, fileLog: null);
        var app = builder.Build();
        HostRoutes.Map(app, RecognizerKind.Fake);
        return app;
    }
}
