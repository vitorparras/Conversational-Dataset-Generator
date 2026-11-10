using Core.Host.Endpoints;
using Core.Platform;
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
        var current = context.Request.Path.Value;
        return new(
            new
            {
                Title = title,
                Screen = screen,
                ScreenParameters = parameters,
                WithSession = withSession,
                Menu = menu,
                reach.Granted,
                reach.Unread,
                Current = current,
                Demonstration = DemonstrationOf(context),
            });
    }

    /// <summary>Whether the installation is a demonstration, which every page says in its header so that nobody sends real material to it.</summary>
    private static bool DemonstrationOf(HttpContext context) => context.RequestServices.GetService<Installation>() is { Kind: InstallationKind.Demonstration };

    /// <summary>
    /// The page of an address that leads nowhere, with 404: the one answer of a route that does not exist and of a screen the function does not reach,
    /// which carries nothing of the address asked, not even as the entry of the menu or of the header it would mark, so the two are the same to the byte.
    /// </summary>
    internal static RazorComponentResult<ScreenFrame> NotFound(HttpContext context)
    {
        var shown = Of(context, typeof(NotFoundScreen), NotFoundScreen.Title);
        var parameters = new Dictionary<string, object?>(shown.Parameters, StringComparer.Ordinal) { [nameof(ScreenFrame.Current)] = null };
        return new(parameters) { StatusCode = StatusCodes.Status404NotFound };
    }

    /// <summary>The page of a failure no route handled, with 500: what happened and what to do, and nothing of the failure.</summary>
    internal static RazorComponentResult<ScreenFrame> Failure(HttpContext context, string? reference = null)
    {
        var shown = new Dictionary<string, object?>(StringComparer.Ordinal) { [nameof(FailureScreen.Reference)] = reference };
        var page = Of(context, typeof(FailureScreen), FailureScreen.Title, shown);
        page.StatusCode = StatusCodes.Status500InternalServerError;
        return page;
    }

    /// <summary>The same page with 503, for a screen whose service the host does not have or whose session ended meanwhile: what to do, and nothing of why.</summary>
    internal static RazorComponentResult<ScreenFrame> Unavailable(HttpContext context)
    {
        var page = Failure(context);
        page.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return page;
    }
}
