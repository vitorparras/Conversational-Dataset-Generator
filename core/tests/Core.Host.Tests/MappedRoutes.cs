using Core.Host.Endpoints;
using Core.Platform.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Core.Host.Tests;

/// <summary>The endpoints the host composition really maps, read from the routing of the built application, and not from the text of the code that maps them.</summary>
internal static class MappedRoutes
{
    private const string ShortCircuitMark = "ShortCircuitMetadata";

    /// <summary>Every route as <c>METHOD /pattern</c>, with whether it is marked open and whether it is short-circuited past the gate.</summary>
    public static IReadOnlyList<MappedRoute> Read(Action<WebApplication>? alsoMapped = null)
    {
        var builder = CompositionRoot.ClosedBuilder(HostProcess.FreePlainAddress().GetLeftPart(UriPartial.Authority));
        CompositionRoot.Compose(builder, ClosedConfiguration.Load(new ConfigurationSchema([HostConfiguration.Section])).View, fileLog: null);
        using var app = builder.Build();
        HostRoutes.Map(app, RecognizerKind.Fake);
        alsoMapped?.Invoke(app);
        return [.. ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().Select(Described)];
    }

    /// <summary>The routes behind the gate, as the arguments of a theory: the method and the route.</summary>
    public static TheoryData<string, string> Gated() =>
        [.. Read().Where(route => !route.Open).Select(route => (route.Method, route.Pattern))];

    private static MappedRoute Described(RouteEndpoint endpoint) =>
        new(
            endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single(),
            endpoint.RoutePattern.RawText!,
            endpoint.Metadata.GetMetadata<SessionOpen>() is not null,
            endpoint.Metadata.Any(mark => mark.GetType().Name == ShortCircuitMark));
}
