using System.Globalization;
using Core.Host.Operators;
using Core.Host.Screens;
using Core.Platform.Sessions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>
/// The pages of the own account, which every one who entered opens (#597, #598): the account with the open sessions, the change of the own password and the
/// confirmations of ending one session or all the others, each on a page of its own (decision 0051). The person is the one the session names, never one the
/// request says; a session is pointed at by the identifier of the list, never by the one of the session. The passwords go from the form to the change and
/// nowhere else: never back to the page, to the address or to the log. An act done takes the browser back to the account with 303, which says what was done.
/// </summary>
internal static class AccountScreensEndpoint
{
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapGet(ScreenPaths.Account, Show).WithMetadata(ScreenAccess.Everyone);
        _ = endpoints.MapGet(ScreenPaths.AccountChange, AskPassword).WithMetadata(ScreenAccess.Everyone);
        _ = endpoints.MapPost(ScreenPaths.AccountChange, ChangePasswordAsync).WithMetadata(ScreenAccess.Everyone);
        _ = endpoints.MapGet(ScreenPaths.AccountEnd, ConfirmEnd).WithMetadata(ScreenAccess.Everyone);
        _ = endpoints.MapPost(ScreenPaths.AccountEnd, EndAsync).WithMetadata(ScreenAccess.Everyone);
        _ = endpoints.MapGet(ScreenPaths.AccountEndOthers, ConfirmEndOthers).WithMetadata(ScreenAccess.Everyone);
        _ = endpoints.MapPost(ScreenPaths.AccountEndOthers, EndOthers).WithMetadata(ScreenAccess.Everyone);
    }

    /// <summary>The account, with what the last act did when the address says so: the address names the act and a count, never anything of a password.</summary>
    private static RazorComponentResult<ScreenFrame> Show(
        HttpContext context, [FromServices] SessionStore sessions, [FromQuery(Name = AccountScreen.DoneField)] string? done, [FromQuery(Name = AccountScreen.EndedField)] int? ended)
    {
        if (Of(context, sessions) is not { } open)
        {
            return ScreenResult.NotFound(context);
        }

        var screen = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(AccountScreen.Sessions)] = open,
            [nameof(AccountScreen.Done)] = done,
            [nameof(AccountScreen.Ended)] = ended ?? 0,
        };
        return ScreenResult.Of(context, typeof(AccountScreen), AccountScreen.Title, screen);
    }

    private static RazorComponentResult<ScreenFrame> AskPassword(HttpContext context) => PasswordPage(context, PasswordScreen.Problem.None);

    private static async Task<IResult> ChangePasswordAsync(HttpContext context, [FromServices] PasswordChange? change)
    {
        var sessions = context.RequestServices.GetRequiredService<SessionStore>();
        var identifier = SessionCookie.Read(context.Request);
        if (change is null || sessions.Resolve(identifier) is not { } account)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var form = context.Request.HasFormContentType ? await context.Request.ReadFormAsync(context.RequestAborted) : null;
        var who = new PasswordChangeRequest(account, identifier, context.Connection.RemoteIpAddress);
        return Answer(context, await change.ChangeAsync(who, Typed(form, PasswordScreen.CurrentField), Typed(form, PasswordScreen.ReplacementField), context.RequestAborted));
    }

    private static string Typed(IFormCollection? form, string field) => form?[field].ToString() ?? string.Empty;

    private static IResult Answer(HttpContext context, PasswordChangeResult result) => result.Outcome switch
    {
        PasswordChangeOutcome.Changed => Back(context, AccountScreen.ChangeDone, result.SessionsEnded),
        PasswordChangeOutcome.EmptyReplacement => PasswordPage(context, PasswordScreen.Problem.EmptyReplacement),
        PasswordChangeOutcome.Refused => PasswordPage(context, PasswordScreen.Problem.Refused),
        PasswordChangeOutcome.NotWritten => PasswordPage(context, PasswordScreen.Problem.NotWritten),
        _ => throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, "The change ended in a way the page does not answer."),
    };

    private static RazorComponentResult<ScreenFrame> PasswordPage(HttpContext context, PasswordScreen.Problem problem) =>
        ScreenResult.Of(context, typeof(PasswordScreen), PasswordScreen.Title, new Dictionary<string, object?>(StringComparer.Ordinal) { [nameof(PasswordScreen.Shown)] = problem });

    /// <summary>The confirmation of ending one session, which names it by when it began and when it was last used; a session not in the list is an address that leads nowhere.</summary>
    private static RazorComponentResult<ScreenFrame> ConfirmEnd(HttpContext context, [FromServices] SessionStore sessions, [FromQuery(Name = SessionEndScreen.SessionField)] string? listed)
    {
        var chosen = Of(context, sessions)?.FirstOrDefault(session => !session.Current && string.Equals(session.ListingId, listed, StringComparison.Ordinal));
        return chosen is null
            ? ScreenResult.NotFound(context)
            : ScreenResult.Of(context, typeof(SessionEndScreen), SessionEndScreen.OneTitle, new Dictionary<string, object?>(StringComparer.Ordinal) { [nameof(SessionEndScreen.Session)] = chosen });
    }

    private static async Task<IResult> EndAsync(HttpContext context, [FromServices] SessionStore sessions)
    {
        var form = context.Request.HasFormContentType ? await context.Request.ReadFormAsync(context.RequestAborted) : null;
        var listed = form?[SessionEndScreen.SessionField].ToString();
        return sessions.Resolve(SessionCookie.Read(context.Request)) is { } account && sessions.CloseListed(account, listed)
            ? Back(context, AccountScreen.SessionsDone, 1)
            : ScreenResult.NotFound(context);
    }

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

    private static IResult Back(HttpContext context, string done, int ended) =>
        PageRedirect.Of(context, $"{ScreenPaths.Account}?{AccountScreen.DoneField}={done}&{AccountScreen.EndedField}={ended.ToString(CultureInfo.InvariantCulture)}");
}
