using Core.Host.Operators;
using Core.Host.Screens;
using Core.Persistence.People;
using Core.Platform.Sessions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>
/// The registration of a person by the screen (#599), with the optional e-mail address (#635): the form calls what the route of JSON calls, and a refusal is the same form again with the
/// error bound to its field and what was typed, never the password; a registration takes the browser to the list filtered by the name just registered.
/// </summary>
internal static class OperatorRegistration
{
    /// <summary>The password is read from the body of the post and goes to the registration, nowhere else: not to the page, the address nor the log.</summary>
    internal static async Task<IResult> RegisterAsync(
        HttpContext context, [FromServices] OperatorAdministration? staff, [FromServices] PersonEmails? emails, [FromServices] SessionStore sessions)
    {
        if (staff is null || sessions.Resolve(SessionCookie.Read(context.Request)) is not { } author)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var form = context.Request.HasFormContentType ? await context.Request.ReadFormAsync(context.RequestAborted) : null;
        var typed = (Name: ValueOf(form, OperatorFormScreen.NameField), Function: ValueOf(form, OperatorFormScreen.FunctionField), Email: AccountEmailEndpoint.Typed(form));
        if (typed.Email is { } given && !PersonEmails.IsAddress(given))
        {
            return Refused(context, typed, new OperatorRefused(AccountEmailScreen.EmailField, AccountEmailScreen.Invalid), StatusCodes.Status400BadRequest);
        }

        var (made, password) = await staff.RegisterAsync(author, new NewPerson(typed.Name, typed.Function), ValueOf(form, OperatorFormScreen.SecretField), context.RequestAborted);
        var registered = OperatorAnswer.Registered(made, password);
        if (registered.Refusal is not { } refusal)
        {
            if (emails is not null)
            {
                await emails.SetAsync(registered.Id!.Value, typed.Email, context.RequestAborted);
            }

            RegisteredNotice.Issue(context.Response, registered.Id!.Value);
            return PageRedirect.Of(context, $"{ScreenPaths.Operators}?{OperatorsScreen.NameKey}={Uri.EscapeDataString(typed.Name)}");
        }

        return Refused(context, typed, refusal, registered.Status);
    }

    /// <summary>The form again, with what was typed, never the password, and the refusal at its field.</summary>
    private static RazorComponentResult<ScreenFrame> Refused(HttpContext context, (string Name, string Function, string? Email) typed, OperatorRefused refusal, int status)
    {
        var screen = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(OperatorFormScreen.Name)] = typed.Name,
            [nameof(OperatorFormScreen.Function)] = typed.Function,
            [nameof(OperatorFormScreen.Email)] = typed.Email,
            [nameof(OperatorFormScreen.ErrorField)] = refusal.Field,
            [nameof(OperatorFormScreen.ErrorReason)] = refusal.Reason,
        };
        var page = ScreenResult.Of(context, typeof(OperatorFormScreen), OperatorFormScreen.Title, screen);
        page.StatusCode = status;
        return page;
    }

    private static string ValueOf(IFormCollection? form, string field) => form is null ? string.Empty : form[field].ToString();
}
