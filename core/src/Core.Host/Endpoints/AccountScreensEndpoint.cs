using Core.Host.Screens;
using Core.Platform.Sessions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>
/// The pages of the own account, which every one who entered opens (#598): the account with the open sessions and the confirmations of ending one session or all the others,
/// each on a page of its own (decision 0051). The person is the one the session names, never one the request says; a session is pointed at by the identifier of the list,
/// never by the one of the session. An act done takes the browser back to the account with 303, which says what was done.
/// </summary>
internal static class AccountScreensEndpoint
{
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapGet(ScreenPaths.Account, Show).WithMetadata(ScreenAccess.Everyone);
        _ = endpoints.MapGet(ScreenPaths.AccountEnd, ConfirmEnd).WithMetadata(ScreenAccess.Everyone);
        _ = endpoints.MapPost(ScreenPaths.AccountEnd, EndAsync).WithMetadata(ScreenAccess.Everyone);
        _ = endpoints.MapGet(ScreenPaths.AccountEndOthers, ConfirmEndOthers).WithMetadata(ScreenAccess.Everyone);
        _ = endpoints.MapPost(ScreenPaths.AccountEndOthers, EndOthers).WithMetadata(ScreenAccess.Everyone);
    }

    /// <summary>The account, with what the last act did when the notice it left says so: the act and a count, and never from the address.</summary>
    private static RazorComponentResult<ScreenFrame> Show(HttpContext context, [FromServices] SessionStore sessions)
    {
        if (Of(context, sessions) is not { } open)
        {
            return ScreenResult.NotFound(context);
        }

        var notice = AccountNotice.Take(context);
        var screen = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(AccountScreen.Sessions)] = open,
            [nameof(AccountScreen.Done)] = notice?.Done,
            [nameof(AccountScreen.Ended)] = notice?.Ended ?? 0,
        };
        return ScreenResult.Of(context, typeof(AccountScreen), AccountScreen.Title, screen);
    }

    /// <summary>The confirmation of ending one session, which names it by when it began and when it was last used; a session not offered by the list is an address that leads nowhere.</summary>
    private static RazorComponentResult<ScreenFrame> ConfirmEnd(HttpContext context, [FromServices] SessionStore sessions, [FromQuery(Name = SessionEndScreen.SessionField)] string? listed)
    {
        var chosen = Offered(context, sessions, listed);
        return chosen is null
            ? ScreenResult.NotFound(context)
            : ScreenResult.Of(context, typeof(SessionEndScreen), SessionEndScreen.OneTitle, new Dictionary<string, object?>(StringComparer.Ordinal) { [nameof(SessionEndScreen.Session)] = chosen });
    }

    /// <summary>
    /// Ends the session the form names, which only a session the list offers can be: the current one, one of another person and one that is gone
    /// are answered as an address that leads nowhere.
    /// </summary>
    private static async Task<IResult> EndAsync(HttpContext context, [FromServices] SessionStore sessions)
    {
        var form = context.Request.HasFormContentType ? await context.Request.ReadFormAsync(context.RequestAborted) : null;
        var chosen = Offered(context, sessions, form?[SessionEndScreen.SessionField].ToString());
        return chosen is not null && sessions.Resolve(SessionCookie.Read(context.Request)) is { } account && sessions.CloseListed(account, chosen.ListingId)
            ? Back(context, AccountScreen.SessionsDone, 1)
            : ScreenResult.NotFound(context);
    }

    /// <summary>The session of the list that the identifier names, among the ones the list offers to end, which leaves out the current one; none when there is no such session.</summary>
    private static SessionListing? Offered(HttpContext context, SessionStore sessions, string? listed) =>
        Of(context, sessions)?.FirstOrDefault(session => !session.Current && string.Equals(session.ListingId, listed, StringComparison.Ordinal));

    private static RazorComponentResult<ScreenFrame> ConfirmEndOthers(HttpContext context, [FromServices] SessionStore sessions) =>
        ScreenResult.Of(context, typeof(SessionEndScreen), SessionEndScreen.OthersTitle, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(SessionEndScreen.Others)] = Of(context, sessions)?.Count(session => !session.Current) ?? 0,
        });

    private static IResult EndOthers(HttpContext context, [FromServices] SessionStore sessions)
    {
        var identifier = SessionCookie.Read(context.Request);
        return sessions.Resolve(identifier) is { } account ? Back(context, AccountScreen.SessionsDone, sessions.CloseOthersOf(account, identifier)) : ScreenResult.NotFound(context);
    }

    /// <summary>The sessions of the one who entered, the current one marked; none when the session is gone meanwhile.</summary>
    private static IReadOnlyList<SessionListing>? Of(HttpContext context, SessionStore sessions)
    {
        var identifier = SessionCookie.Read(context.Request);
        return sessions.Resolve(identifier) is { } account ? sessions.ListOf(account, identifier) : null;
    }

    private static IResult Back(HttpContext context, string done, int ended)
    {
        AccountNotice.Issue(context.Response, done, ended);
        return PageRedirect.Of(context, ScreenPaths.Account);
    }
}
