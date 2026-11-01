using Core.Platform.Sessions;

namespace Core.Host.Endpoints;

/// <summary>
/// Stands before every route that requires a session and lets through only a request whose cookie names a session the store still knows,
/// which this use keeps alive. Every other request gets 401 with the route of the entry; taking the person there without losing
/// what they had submitted waits for the interface.
/// </summary>
internal static class SessionGate
{
    internal static void Use(IApplicationBuilder app) =>
        _ = app.Use(async (context, next) =>
        {
            var required = context.GetEndpoint()?.Metadata.GetMetadata<SessionRequired>();
            if (required is not null && context.RequestServices.GetRequiredService<SessionStore>().Resolve(SessionCookie.Read(context.Request)) is null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new SessionRefused(required.SignIn)).ConfigureAwait(false);
                return;
            }

            await next(context).ConfigureAwait(false);
        });
}
