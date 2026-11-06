using Core.Platform.Sessions;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>
/// The entry (POST), the exit (DELETE) and the probe of a live session (GET) of the person at the host. The entry answers 401 only for a refusal of the credential;
/// a right password that the chain was too busy to record is answered 503 with the reason.
/// </summary>
internal static class SessionEndpoint
{
    internal const string Route = "/session";
    private const string Unavailable = "Outra gravação ocupou a cadeia de custódia e a entrada não pôde ser registrada; nada foi recusado, e a entrada pode ser repetida.";

    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapPost(Route, EnterAsync).WithMetadata(new SessionOpen());
        _ = endpoints.MapDelete(Route, Leave);
        _ = endpoints.MapGet(Route, () => Results.NoContent());
    }

    /// <summary>A refusal says the same whatever its cause; with no database declared there is nobody to enter, and the answer says it is the host that cannot.</summary>
    private static async Task<IResult> EnterAsync(
        [FromBody] SignInAsked asked, [FromServices] SignIn? signIn, HttpContext context, CancellationToken cancellationToken)
    {
        if (signIn is null)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var entry = await signIn.OpenAsync(asked.Name, asked.Password, context.Connection.RemoteIpAddress, cancellationToken);
        if (entry.ChainBusy)
        {
            return Results.Json(new SessionUnavailable(Unavailable), statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (entry.Identifier is not { } identifier)
        {
            return Results.Json(new SessionRefused(Route), statusCode: StatusCodes.Status401Unauthorized);
        }

        SessionCookie.Issue(context.Response, identifier);
        return Results.NoContent();
    }

    private static IResult Leave(HttpContext context, [FromServices] SessionStore sessions)
    {
        sessions.Close(SessionCookie.Read(context.Request));
        SessionCookie.Clear(context.Response);
        return Results.NoContent();
    }
}
