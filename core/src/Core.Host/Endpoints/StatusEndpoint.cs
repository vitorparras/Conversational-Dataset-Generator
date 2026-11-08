using Core.Persistence.People;
using Core.Platform.Sessions;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>
/// Answers whether the process is alive and whether it is ready, to anyone, which is what the probes of health read. The detail (the recognizer, the language service, each startup
/// check, the installation, the comparison of the server's log and the warning of the seal) goes only to a session of the technical administration or the audit, asked as the screen
/// gate asks (#632); a session of another function gets the answer of a route that does not exist.
/// </summary>
internal static class StatusEndpoint
{
    internal const string Route = "/status";

    private static readonly ScreenAccess Detail = ScreenAccess.Unlisted(PersonFunctions.Administration, PersonFunctions.Audit);

    internal static void Map(IEndpointRouteBuilder endpoints, RecognizerKind recognizer) =>
        _ = endpoints.MapGet(
            Route,
            (HttpContext context, [AsParameters] StatusSources sources, [FromServices] SessionStore sessions) =>
                ShowAsync(context, SystemState.Status(sources, recognizer), sessions)).WithMetadata(new SessionOpen());

    private static async Task<IResult> ShowAsync(HttpContext context, StatusResponse status, SessionStore sessions)
    {
        if (sessions.Resolve(SessionCookie.Read(context.Request)) is null)
        {
            return Results.Ok(new StatusSummary(status.Alive, status.Ready));
        }

        if ((await ScreenGate.ReachOfAsync(context)).Reaches(Detail))
        {
            return Results.Ok(status);
        }

        await ScreenGate.NotFoundAsync(context);
        return Results.Empty;
    }
}
