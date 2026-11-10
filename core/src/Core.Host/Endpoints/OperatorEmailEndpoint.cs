using System.Globalization;
using Core.Host.Operators;
using Core.Host.Screens;
using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Platform.Sessions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>
/// The e-mail address of another person, which the technical administration sees and corrects after the registration (#635, decision 11 of the owner): the corrected address goes from the
/// form to the registry, with the act recorded in the chain without it, and back to this page alone, never to the address of a page or to the log. A saving takes the browser back to the
/// page with 303, which says it was saved; the person sees the new address on their own account. A deactivated person has no address, and the page says so without a form.
/// </summary>
internal static class OperatorEmailEndpoint
{
    private const string SavedKey = "guardado";

    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapGet(ScreenPaths.OperatorEmail, ShowAsync).WithMetadata(ScreenAccess.Unlisted(PersonFunctions.Administration));
        _ = endpoints.MapPost(ScreenPaths.OperatorEmail, CorrectAsync).WithMetadata(ScreenAccess.Unlisted(PersonFunctions.Administration));
    }

    /// <summary>The address of the page of the e-mail of a person.</summary>
    internal static string Of(long id) => ScreenPaths.OperatorEmail.Replace("{id}", id.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static async Task<IResult> ShowAsync(long id, HttpContext context, [FromServices] OperatorAdministration? staff, [FromServices] PersonEmails? emails)
    {
        if (staff is null || emails is null)
        {
            return ScreenResult.Unavailable(context);
        }

        if (await staff.Registry.NameOfAsync(id, context.RequestAborted) is not { } name)
        {
            return ScreenResult.NotFound(context);
        }

        var active = await staff.Registry.ActiveFunctionOfAsync(id, context.RequestAborted) is not null;
        var address = active ? await emails.AddressOfAsync(id, context.RequestAborted) : null;
        return Page(context, new OperatorEmailScreen.Shown(id, name, active, address), context.Request.Query.ContainsKey(SavedKey), error: null);
    }

    private static async Task<IResult> CorrectAsync(
        long id, HttpContext context, [FromServices] OperatorAdministration? staff, [FromServices] PersonEmails? emails)
    {
        var sessions = context.RequestServices.GetRequiredService<SessionStore>();
        if (staff is null || emails is null || sessions.Resolve(SessionCookie.Read(context.Request)) is not { } author)
        {
            return ScreenResult.Unavailable(context);
        }

        if (await staff.Registry.NameOfAsync(id, context.RequestAborted) is not { } name)
        {
            return ScreenResult.NotFound(context);
        }

        var address = AccountEmailEndpoint.Typed(context.Request.HasFormContentType ? await context.Request.ReadFormAsync(context.RequestAborted) : null);
        if (address is not null && !PersonEmails.IsAddress(address))
        {
            return WithStatus(Page(context, new OperatorEmailScreen.Shown(id, name, Active: true, address), saved: false, AccountEmailScreen.Invalid), StatusCodes.Status400BadRequest);
        }

        var corrected = await emails.CorrectAsync(id, address, CustodyAuthor.Person(author), context.RequestAborted);
        if (corrected.Outcome == PersonOutcome.Deactivated)
        {
            return WithStatus(Page(context, new OperatorEmailScreen.Shown(id, name, Active: false, Address: null), saved: false, error: null), StatusCodes.Status409Conflict);
        }

        return corrected.Outcome == PersonOutcome.Done ? PageRedirect.Of(context, $"{Of(id)}?{SavedKey}") : ScreenResult.Failure(context);
    }

    private static RazorComponentResult WithStatus(RazorComponentResult page, int status)
    {
        page.StatusCode = status;
        return page;
    }

    private static RazorComponentResult<ScreenFrame> Page(HttpContext context, OperatorEmailScreen.Shown target, bool saved, string? error) =>
        ScreenResult.Of(context, typeof(OperatorEmailScreen), OperatorEmailScreen.Title, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(OperatorEmailScreen.Subject)] = target,
            [nameof(OperatorEmailScreen.Saved)] = saved,
            [nameof(OperatorEmailScreen.Error)] = error,
        });
}
