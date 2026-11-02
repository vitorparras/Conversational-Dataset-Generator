namespace Zqx.Fixtures;

internal static class OpenElsewhere
{
    internal static void Map(dynamic app)
    {
        app.MapGet("/other", () => 1).WithMetadata(new SessionOpen());
    }
}
