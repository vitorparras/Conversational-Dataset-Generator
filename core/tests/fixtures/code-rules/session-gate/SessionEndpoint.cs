namespace Zqx.Fixtures;

internal static class EntryRoute
{
    internal static void Map(dynamic app) => app.MapPost("/open", () => 1).WithMetadata(new SessionOpen());
}
