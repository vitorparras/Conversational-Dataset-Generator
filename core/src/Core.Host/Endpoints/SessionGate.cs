using Core.Platform.Sessions;

namespace Core.Host.Endpoints;

/// <summary>
/// Stands before every route and lets through only a request whose cookie names a session the store still knows, which this use keeps alive;
/// the routes marked <see cref="SessionOpen"/> are the only ones with no session to ask. A request that presents the identifier without
/// encryption is refused and ends the session, because an identifier that travelled in clear may have been seen. Every refusal is the same for every cause:
/// a request for a page (a browser asking for HTML with a GET or a form) goes to the entry with 303, and that holds for a route that does not exist as well,
/// so that going there reveals nothing about which pages exist; any other request gets 401 in JSON with the route of the entry.
/// </summary>
internal static class SessionGate
{
    private const string Html = "text/html";

    internal static void Use(IApplicationBuilder app) =>
        _ = app.Use(async (context, next) =>
        {
            var sessions = context.RequestServices.GetRequiredService<SessionStore>();
            var presented = SessionCookie.Read(context.Request);
            if (presented is not null && !context.Request.IsHttps)
            {
                sessions.Close(presented);
                await RefuseAsync(context).ConfigureAwait(false);
                return;
            }

            if (NeedsSession(context) && sessions.Resolve(presented) is null)
            {
                await RefuseAsync(context).ConfigureAwait(false);
                return;
            }

            await next(context).ConfigureAwait(false);
        });

    /// <summary>
    /// A route that exists asks for a session unless it is marked open; one that does not exist asks for one only of a request for a page, which is sent to the entry,
    /// and is 404 otherwise.
    /// </summary>
    private static bool NeedsSession(HttpContext context) =>
        context.GetEndpoint() is { } endpoint ? endpoint.Metadata.GetMetadata<SessionOpen>() is null : AsksForPage(context.Request);

    private static bool AsksForPage(HttpRequest request) =>
        (HttpMethods.IsGet(request.Method) || (HttpMethods.IsPost(request.Method) && request.HasFormContentType))
        && request.GetTypedHeaders().Accept.Any(accepted => accepted.MediaType.Equals(Html, StringComparison.OrdinalIgnoreCase));

    private static Task RefuseAsync(HttpContext context)
    {
        if (AsksForPage(context.Request))
        {
            PageRedirect.To(context.Response, ScreenPaths.SignIn);
            return Task.CompletedTask;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return context.Response.WriteAsJsonAsync(new SessionRefused(SessionEndpoint.Route), context.RequestAborted);
    }
}
