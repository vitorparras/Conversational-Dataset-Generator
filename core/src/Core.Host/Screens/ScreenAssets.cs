using Core.Host.Endpoints;

namespace Core.Host.Screens;

/// <summary>
/// The script of the client layer and the style of the screens, carried inside the executable (the publish turns the static web assets off) and
/// served by routes of their own. They stand before the entry, so they ask for no session; they hold no data of anyone.
/// The style is the tokens followed by the components, in one response, so that no component is read before the values it names.
/// </summary>
internal static class ScreenAssets
{
    internal const string ScriptRoute = "/screens/client.js";
    internal const string StyleRoute = "/screens/client.css";

    private static readonly IEnumerable<string> Script = Enumerable.Repeat("Screens/client.js", 1);
    private static readonly IEnumerable<string> Style = Enumerable.Repeat("Screens/tokens.css", 1).Append("Screens/screens.css");

    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapGet(ScriptRoute, context => ServeAsync(context, Script, "text/javascript"))
            .WithMetadata(new SessionOpen("the page that makes the session needs its script"));
        _ = endpoints.MapGet(StyleRoute, context => ServeAsync(context, Style, "text/css"))
            .WithMetadata(new SessionOpen("the page that makes the session needs its style"));
    }

    private static async Task ServeAsync(HttpContext context, IEnumerable<string> resources, string contentType)
    {
        context.Response.ContentType = $"{contentType}; charset=utf-8";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers.CacheControl = "no-cache";
        foreach (var resource in resources)
        {
            await using var content = typeof(ScreenAssets).Assembly.GetManifestResourceStream(resource)!;
            await content.CopyToAsync(context.Response.Body, context.RequestAborted).ConfigureAwait(false);
        }
    }
}
