using Core.Host.Endpoints;

namespace Core.Host.Screens;

/// <summary>
/// The menu of whoever entered, made from the routes of the screens and the same question the screen gate asks of the route (CA-3 of #594), in the place
/// each screen declares. The menu only shows the way: a route left out of it still refuses on its own.
/// </summary>
internal static class ScreenMenu
{
    internal static List<ScreenNav.Entry> Of(HttpContext context, ScreenReach reach)
    {
        var routes = context.RequestServices.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Select(route => (Route: route, Access: route.Metadata.GetMetadata<ScreenAccess>()))
            .Where(screen => screen.Access?.Caption is not null && reach.Reaches(screen.Access))
            .OrderBy(screen => screen.Access!.Place)
            .Select(screen => new ScreenNav.Entry(screen.Access!.Caption!, screen.Route.RoutePattern.RawText ?? ScreenPaths.Home));
        var entries = new List<ScreenNav.Entry>(routes);
        return entries;
    }
}
