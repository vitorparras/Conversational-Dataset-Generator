using Core.Host.Screens;
using Core.Persistence.People;
using Core.Platform.Sessions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>
/// The e-mail address of the own account (#635, CA-4), which every one who entered opens: the person is the one the session names, never one the request says. The address goes from the form
/// to the registry and back to this page alone, never to the address of a page or to the log; a saving takes the browser back to the page with 303, which says it was saved.
/// </summary>
internal static class AccountEmailEndpoint
{
    private const string SavedKey = "guardado";

    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapGet(ScreenPaths.AccountEmail, ShowAsync).WithMetadata(ScreenAccess.Everyone);
        _ = endpoints.MapPost(ScreenPaths.AccountEmail, SaveAsync).WithMetadata(ScreenAccess.Everyone);
    }

    /// <summary>The address given in a form, trimmed; none when the field is blank, which removes the address.</summary>
    internal static string? Typed(IFormCollection? form)
    {
        var typed = form?[AccountEmailScreen.EmailField].ToString().Trim();
        return string.IsNullOrEmpty(typed) ? null : typed;
    }

    private static async Task<IResult> ShowAsync(HttpContext context, [FromServices] PersonEmails? emails, [FromServices] SessionStore sessions)
    {
        if (emails is null || sessions.Resolve(SessionCookie.Read(context.Request)) is not { } person)
        {
            return ScreenResult.Unavailable(context);
        }

        return Page(context, await emails.AddressOfAsync(person, context.RequestAborted), context.Request.Query.ContainsKey(SavedKey), error: null);
    }

    private static async Task<IResult> SaveAsync(HttpContext context, [FromServices] PersonEmails? emails, [FromServices] SessionStore sessions)
    {
        if (emails is null || sessions.Resolve(SessionCookie.Read(context.Request)) is not { } person)
        {
            return ScreenResult.Unavailable(context);
        }

        var address = Typed(context.Request.HasFormContentType ? await context.Request.ReadFormAsync(context.RequestAborted) : null);
        if (address is not null && !PersonEmails.IsAddress(address))
        {
            var refused = Page(context, address, saved: false, AccountEmailScreen.Invalid);
            refused.StatusCode = StatusCodes.Status400BadRequest;
            return refused;
        }

        await emails.SetAsync(person, address, context.RequestAborted);
        return PageRedirect.Of(context, $"{ScreenPaths.AccountEmail}?{SavedKey}");
    }

    private static RazorComponentResult<ScreenFrame> Page(HttpContext context, string? address, bool saved, string? error) =>
        ScreenResult.Of(context, typeof(AccountEmailScreen), AccountEmailScreen.Title, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(AccountEmailScreen.Address)] = address,
            [nameof(AccountEmailScreen.Saved)] = saved,
            [nameof(AccountEmailScreen.Error)] = error,
        });
}
