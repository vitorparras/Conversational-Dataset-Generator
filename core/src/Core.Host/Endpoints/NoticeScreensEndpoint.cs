using System.Globalization;
using Core.Host.Operators;
using Core.Host.Screens;
using Core.Platform.Sessions;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>
/// The notices of the person of the session (#595): the list of the unread, and the mark that reads one, posted from the list and answered with a redirect to it. Everyone who entered reaches it,
/// whatever the function, since a notice is about the own account, and it stays out of the menu of work: the header says how many are unread.
/// Nobody lists or marks another person's, and a notice that is not theirs is answered as one that does not exist.
/// </summary>
internal static class NoticeScreensEndpoint
{
    private const string ReadMark = "lido";
    private const string ReadPattern = $"{ScreenPaths.Notices}/{{id}}/{ReadMark}";

    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapGet(ScreenPaths.Notices, ListAsync).WithMetadata(ScreenAccess.Everyone);
        _ = endpoints.MapPost(ReadPattern, ReadAsync).WithMetadata(ScreenAccess.Everyone);
    }

    /// <summary>Where the form of a notice posts to read it.</summary>
    internal static string ReadPath(long id) => string.Create(CultureInfo.InvariantCulture, $"{ScreenPaths.Notices}/{id}/{ReadMark}");

    private static async Task<IResult> ListAsync(HttpContext context, [FromServices] OperatorAdministration? staff, [FromServices] SessionStore sessions)
    {
        if (staff is null)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (sessions.Resolve(SessionCookie.Read(context.Request)) is not { } person)
        {
            return ScreenResult.NotFound(context);
        }

        var screen = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(NoticeScreen.Notices)] = await staff.Notices.UnreadAsync(person, context.RequestAborted),
            [nameof(NoticeScreen.Marked)] = context.Request.Query.ContainsKey(ReadMark),
        };
        return ScreenResult.Of(context, typeof(NoticeScreen), NoticeScreen.Title, screen);
    }

    private static async Task<IResult> ReadAsync(string id, HttpContext context, [FromServices] OperatorAdministration? staff, [FromServices] SessionStore sessions)
    {
        if (staff is null)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var marked = sessions.Resolve(SessionCookie.Read(context.Request)) is { } person
            && long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var notice)
            && await staff.Notices.MarkReadAsync(person, notice, context.RequestAborted);
        return marked ? PageRedirect.Of(context, $"{ScreenPaths.Notices}?{ReadMark}") : ScreenResult.NotFound(context);
    }
}
