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
    /// A route not marked open stands behind the session gate, so its page has a session to end; the menu and the domains are what the screen gate read.
    /// </summary>
    internal static RazorComponentResult<ScreenFrame> Of(HttpContext context, Type screen, string title, IDictionary<string, object?>? parameters = null)
    {
        context.Response.Headers.CacheControl = "no-store";
        var withSession = context.GetEndpoint()?.Metadata.GetMetadata<SessionOpen>() is null;
        var reach = context.Features.Get<ScreenReach>() ?? ScreenReach.Nothing;
        var menu = ScreenMenu.Of(context, reach);
        return new(new { Title = title, Screen = screen, ScreenParameters = parameters, WithSession = withSession, Menu = menu, reach.Granted, reach.Unread, Current = context.Request.Path.Value });
    }

    /// <summary>
    /// The page of an address that leads nowhere, with 404: the one answer of a route that does not exist and of a screen the function does not reach,
    /// which carries nothing of the address asked, so the two are the same to the byte.
    /// </summary>
    internal static RazorComponentResult<ScreenFrame> NotFound(HttpContext context)
    {
        var page = Of(context, typeof(NotFoundScreen), NotFoundScreen.Title);
        page.StatusCode = StatusCodes.Status404NotFound;
        return page;
    }

    /// <summary>The page of a failure no route handled, with 500: what happened and what to do, and nothing of the failure.</summary>
    internal static RazorComponentResult<ScreenFrame> Failure(HttpContext context)
    {
        var page = Of(context, typeof(FailureScreen), FailureScreen.Title);
        page.StatusCode = StatusCodes.Status500InternalServerError;
        return page;
    }
}
