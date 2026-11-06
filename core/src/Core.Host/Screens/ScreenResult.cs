using Core.Host.Endpoints;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Host.Screens;

/// <summary>
/// What a route returns for a screen: always the whole page, so that an answer the client layer cannot swap is still shown with its frame;
/// the client layer takes out of it the region it swaps.
/// </summary>
internal static class ScreenResult
{
    /// <summary>
    /// The screen of the type given, a component, with the title of its page and what it takes. No cache keeps it: a page carries what one person may see.
    /// A route not marked open stands behind the session gate, so its page has a session to end.
    /// </summary>
    internal static RazorComponentResult<ScreenFrame> Of(HttpContext context, Type screen, string title, IDictionary<string, object?>? parameters = null)
    {
        context.Response.Headers.CacheControl = "no-store";
        var withSession = context.GetEndpoint()?.Metadata.GetMetadata<SessionOpen>() is null;
        return new(new { Title = title, Screen = screen, ScreenParameters = parameters, WithSession = withSession });
    }
}
