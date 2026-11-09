using Core.Host.Operators;
using Core.Host.Screens;
using Core.Platform.Sessions;

namespace Core.Host.Endpoints;

/// <summary>
/// Stands after the session gate and holds to the access policy (006) every request to a screen and every request for a page behind the session. It reads the function
/// of whoever entered from the registry, and the domains granted from the <see cref="DomainGrants"/> registered, and keeps them for the page, whose menu asks the same question.
/// A screen the function does not reach, a page asked of a route that declares no access and a page asked of a route that does not exist get one answer, from
/// <see cref="NotFoundAsync"/>: 404, with the page of an address that leads nowhere when a page was asked for and with no body otherwise, so nothing tells them apart.
/// </summary>
internal static class ScreenGate
{
    internal static void Use(IApplicationBuilder app) =>
        _ = app.Use(async (context, next) =>
        {
            var endpoint = context.GetEndpoint();
            var access = endpoint?.Metadata.GetMetadata<ScreenAccess>();
            if (endpoint?.Metadata.GetMetadata<SessionOpen>() is not null || (access is null && !SessionGate.AsksForPage(context.Request)))
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            var reach = await ReachOfAsync(context).ConfigureAwait(false);
            context.Features.Set(reach);
            await (endpoint is not null && reach.Reaches(access) ? next(context) : NotFoundAsync(context)).ConfigureAwait(false);
        });

    /// <summary>The one answer of an address that leads nowhere the one who entered may go.</summary>
    internal static Task NotFoundAsync(HttpContext context)
    {
        if (!SessionGate.AsksForPage(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        }

        return ScreenResult.NotFound(context).ExecuteAsync(context);
    }

    /// <summary>With no database declared there is no registry, so no function and no screen but the pages of everyone.</summary>
    internal static async Task<ScreenReach> ReachOfAsync(HttpContext context)
    {
        var subject = context.RequestServices.GetRequiredService<SessionStore>().Resolve(SessionCookie.Read(context.Request));
        if (subject is not { } entered || context.RequestServices.GetService<OperatorAdministration>() is not { } administration)
        {
            return ScreenReach.Nothing;
        }

        var function = await administration.Registry.ActiveFunctionOfAsync(entered, context.RequestAborted).ConfigureAwait(false);
        var grants = context.RequestServices.GetService<DomainGrants>();
        var granted = grants is null ? DomainGrants.None : await grants.GrantedToAsync(entered, context.RequestAborted).ConfigureAwait(false);
        var unread = await administration.Notices.UnreadCountAsync(entered, context.RequestAborted).ConfigureAwait(false);
        return new ScreenReach(function, granted) { Unread = unread };
    }
}
