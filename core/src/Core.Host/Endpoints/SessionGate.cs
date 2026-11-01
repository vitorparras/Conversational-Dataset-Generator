using Core.Platform.Sessions;

namespace Core.Host.Endpoints;

/// <summary>
/// Stands before every route and lets through only a request whose cookie names a session the store still knows, which this use keeps alive;
/// the routes marked <see cref="SessionOpen"/> are the only ones with no session to ask. A request that presents the identifier without
/// encryption is refused and ends the session, because an identifier that travelled in clear may have been seen. Every refusal gets 401 with the
/// route of the entry, the same for every cause; taking the person there without losing what they had submitted waits for the interface.
/// </summary>
internal static class SessionGate
{
    internal static void Use(IApplicationBuilder app) =>
        _ = app.Use(async (context, next) =>
        {
            var sessions = context.RequestServices.GetRequiredService<SessionStore>();
            var presented = SessionCookie.Read(context.Request);
            if (presented is not null && !context.Request.IsHttps)
            {
                sessions.Close(presented);
                await RefuseAsync(context.Response).ConfigureAwait(false);
                return;
            }

            if (context.GetEndpoint() is { } endpoint && endpoint.Metadata.GetMetadata<SessionOpen>() is null && sessions.Resolve(presented) is null)
            {
                await RefuseAsync(context.Response).ConfigureAwait(false);
                return;
            }

            await next(context).ConfigureAwait(false);
        });

    private static Task RefuseAsync(HttpResponse response)
    {
        response.StatusCode = StatusCodes.Status401Unauthorized;
        return response.WriteAsJsonAsync(new SessionRefused(SessionEndpoint.Route));
    }
}
