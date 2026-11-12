using Core.Host.Screens;
using Core.Platform.Sessions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>
/// The pages of the entry and of the exit, which a browser walks through with forms and not with JSON: the entry page and its form (open, because it makes the session),
/// the exit (behind the gate, because it ends one) and the first page after the entry. A refusal is the same page again with the name typed and never the password,
/// whatever its cause, except a right password the chain was too busy to record, which is the same page with 503 and a notice that nothing was refused;
/// a success and an exit take the browser on with 303. The password is read from the body of the post and goes to the store of credentials, nowhere else.
/// </summary>
internal static class EntryPageEndpoint
{
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapGet(ScreenPaths.SignIn, ShowAsync).WithMetadata(new SessionOpen("the page that makes the session"));
        _ = endpoints.MapPost(ScreenPaths.SignIn, EnterAsync).WithMetadata(new SessionOpen("the form that makes the session"));
        _ = endpoints.MapPost(ScreenPaths.SignOut, Leave).WithMetadata(ScreenAccess.Everyone);
        _ = endpoints.MapGet(ScreenPaths.Home, Start).WithMetadata(ScreenAccess.Everyone);
    }

    private static async Task<IResult> ShowAsync(HttpContext context, [FromServices] SignIn? signIn) =>
        signIn is null ? Results.StatusCode(StatusCodes.Status503ServiceUnavailable) : await PageAsync(context, signIn, name: null, EntryNotice.None);

    /// <summary>With no database declared there is nobody to enter, and the answer says it is the host that cannot, as the route of JSON does.</summary>
    private static async Task<IResult> EnterAsync(HttpContext context, [FromServices] SignIn? signIn)
    {
        if (signIn is null)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var form = context.Request.HasFormContentType ? await context.Request.ReadFormAsync(context.RequestAborted) : null;
        var name = form?[SignInScreen.NameField].ToString() ?? string.Empty;
        var password = form?[SignInScreen.SecretField].ToString() ?? string.Empty;
        var entry = await signIn.OpenAsync(new SignInRequest(name, password, context.Connection.RemoteIpAddress, context.Request.Headers.UserAgent.ToString()), context.RequestAborted);
        if (entry.ChainBusy)
        {
            return await PageAsync(context, signIn, name, EntryNotice.Busy);
        }

        if (entry.Identifier is not { } identifier)
        {
            return await PageAsync(context, signIn, name, EntryNotice.Refused);
        }

        SessionCookie.Issue(context.Response, identifier);
        return PageRedirect.Of(context, ScreenPaths.Home);
    }

    /// <summary>The notice the page shows above the form: none, the refusal of the entry, or the chain too busy to record a right password.</summary>
    private enum EntryNotice
    {
        None,
        Refused,
        Busy,
    }

    private static async Task<IResult> PageAsync(HttpContext context, SignIn signIn, string? name, EntryNotice notice)
    {
        var unregistered = await signIn.NobodyRegisteredAsync(context.RequestAborted);
        var screen = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(SignInScreen.Name)] = name,
            [nameof(SignInScreen.Refused)] = notice == EntryNotice.Refused,
            [nameof(SignInScreen.Unavailable)] = notice == EntryNotice.Busy,
            [nameof(SignInScreen.Unregistered)] = unregistered,
        };
        var page = ScreenResult.Of(context, typeof(SignInScreen), SignInScreen.Title, screen);
        if (notice == EntryNotice.Busy)
        {
            page.StatusCode = StatusCodes.Status503ServiceUnavailable;
        }

        return page;
    }

    private static IResult Leave(HttpContext context, [FromServices] SessionStore sessions)
    {
        sessions.Close(SessionCookie.Read(context.Request));
        SessionCookie.Clear(context.Response);
        return PageRedirect.Of(context, ScreenPaths.SignIn);
    }

    private static RazorComponentResult<ScreenFrame> Start(HttpContext context) => ScreenResult.Of(context, typeof(HomeScreen), "Início");
}
