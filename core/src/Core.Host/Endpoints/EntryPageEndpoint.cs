using Core.Host.Screens;
using Core.Platform.Sessions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>
/// The pages of the entry and of the exit, which a browser walks through with forms and not with JSON: the entry page and its form (open, because it makes the session),
/// the exit (behind the gate, because it ends one) and the first page after the entry. A refusal is the same page again with the name typed and never the password,
/// whatever its cause; a success and an exit take the browser on with 303. The password is read from the body of the post and goes to the store of credentials, nowhere else.
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
        signIn is null ? Results.StatusCode(StatusCodes.Status503ServiceUnavailable) : await PageAsync(context, signIn, name: null, refused: false);

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
        if (await signIn.OpenAsync(name, password, context.Connection.RemoteIpAddress, context.RequestAborted) is not { } identifier)
        {
            return await PageAsync(context, signIn, name, refused: true);
        }

        SessionCookie.Issue(context.Response, identifier);
        return PageRedirect.Of(context, ScreenPaths.Home);
    }

    private static async Task<IResult> PageAsync(HttpContext context, SignIn signIn, string? name, bool refused)
    {
        var unregistered = await signIn.NobodyRegisteredAsync(context.RequestAborted);
        var screen = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(SignInScreen.Name)] = name,
            [nameof(SignInScreen.Refused)] = refused,
            [nameof(SignInScreen.Unregistered)] = unregistered,
        };
        return ScreenResult.Of(context, typeof(SignInScreen), "Entrada", screen);
    }

    private static IResult Leave(HttpContext context, [FromServices] SessionStore sessions)
    {
        sessions.Close(SessionCookie.Read(context.Request));
        SessionCookie.Clear(context.Response);
        return PageRedirect.Of(context, ScreenPaths.SignIn);
    }

    private static RazorComponentResult<ScreenFrame> Start(HttpContext context) => ScreenResult.Of(context, typeof(HomeScreen), "Início");
}
