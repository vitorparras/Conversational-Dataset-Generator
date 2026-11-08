using Core.Host.Operators;
using Core.Platform.Sessions;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>
/// The sessions of the person of the session, to list and to end one by the identifier of the list, and the change of the own password. Nobody reaches another person's: the person is the one
/// the session names, never one a request says. With no database declared the change cannot be done, and the host says so.
/// </summary>
internal static class AccountEndpoint
{
    internal const string Route = "/account";

    private const string WrongCurrent = "A senha atual não confere.";
    private const string BlankSecret = "A nova senha não pode ser vazia.";

    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapGet(Route + "/sessions", ListSessions);
        _ = endpoints.MapDelete(Route + "/sessions/{id}", EndSession);
        _ = endpoints.MapPost(Route + "/password", ChangeAsync);
    }

    private static IResult ListSessions(HttpContext context, [FromServices] SessionStore sessions)
    {
        var current = SessionCookie.Read(context.Request);
        return sessions.Resolve(current) is { } account ? Results.Ok(Shown(sessions.ListOf(account, current))) : Results.NotFound();
    }

    private static IEnumerable<AccountSessionShown> Shown(IEnumerable<SessionListing> listed) =>
        listed.Select(session => new AccountSessionShown(session.ListingId, session.Start, session.LastActivity, session.Current));

    private static IResult EndSession(string id, HttpContext context, [FromServices] SessionStore sessions) =>
        sessions.Resolve(SessionCookie.Read(context.Request)) is { } account && sessions.CloseListed(account, id) ? Results.NoContent() : Results.NotFound();

    private static async Task<IResult> ChangeAsync(
        [FromBody] PasswordChangeAsked asked, [FromServices] PasswordChange? change, HttpContext context, CancellationToken cancellationToken)
    {
        if (change is null)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var sessions = context.RequestServices.GetRequiredService<SessionStore>();
        var identifier = SessionCookie.Read(context.Request);
        if (sessions.Resolve(identifier) is not { } account)
        {
            return Results.NotFound();
        }

        var who = new PasswordChangeRequest(account, identifier, context.Connection.RemoteIpAddress);
        return Answer(await change.ChangeAsync(who, asked.Current ?? string.Empty, asked.Replacement ?? string.Empty, cancellationToken));
    }

    private static IResult Answer(PasswordChangeResult result) => result.Outcome switch
    {
        PasswordChangeOutcome.Changed => Results.Ok(new PasswordChanged(result.SessionsEnded)),
        PasswordChangeOutcome.EmptyReplacement => OperatorAnswer.AtField("senha-nova", BlankSecret),
        PasswordChangeOutcome.NotWritten => OperatorAnswer.Of(result.Failure!),
        PasswordChangeOutcome.Refused => OperatorAnswer.AtField("senha-atual", WrongCurrent),
        _ => throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, "The change ended in a way the route does not answer."),
    };
}
