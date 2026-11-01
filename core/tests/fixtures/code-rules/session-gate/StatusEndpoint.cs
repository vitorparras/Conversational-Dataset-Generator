namespace Zqx.Fixtures;

internal static class AliveRoute
{
    internal static void Map(dynamic app) => app.MapGet("/alive", () => 1).WithMetadata(new SessionOpen());
}
