namespace Zqx.Fixtures;

internal static class RouteSet
{
    internal static void Map(dynamic app)
    {
        StatusRoute.Map(app);
        Map(app, 1);
    }

    internal static void Map(dynamic app, int again) => app.MapGet("/z", () => again);
}

internal static class StatusRoute
{
    internal static void Map(dynamic app) => app.MapGet("/status", () => 1);
}
