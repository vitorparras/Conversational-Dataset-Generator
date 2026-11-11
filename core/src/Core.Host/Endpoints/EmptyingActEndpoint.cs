using Core.Host.Screens;
using Core.Persistence.Content.Emptying;
using Core.Platform.Sessions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>
/// The confirmation of the emptying and the act (#617, decision 0051). The confirmation is a page of its own that says what is lost and asks for the motive
/// and the protocol; the act is the POST of that page,
/// which ends in a 303 to the entry, and comes back on the confirmation, with what was typed, when the motive is missing, the protocol is not a number or the chain did not take the event.
/// An item that is not there is answered by the entry form itself (no 303, nothing was written), with the refusal on the field.
/// </summary>
internal static class EmptyingActEndpoint
{
    /// <summary>The confirmation of the item of the address, or the entry when it is not a number or the item is emptied already.</summary>
    internal static async Task<IResult> ConfirmAsync(HttpContext context, [FromServices] ItemEmptyingDesk? desk)
    {
        if (desk is null)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var domain = await EmptyingScreensEndpoint.ActiveAsync(context, desk);
        if (domain is null)
        {
            return ScreenResult.NotFound(context);
        }

        var typed = context.Request.Query[EmptyingScreen.ItemField].ToString();
        if (!EmptyingScreensEndpoint.TryItem(typed, out var item) || await desk.RecordOfAsync(domain.Id, item, context.RequestAborted) is not null)
        {
            return PageRedirect.Of(context, EmptyingScreensEndpoint.Back(typed));
        }

        return Confirmation(context, (domain, item), new Asked(null, null, Problem.None));
    }

    /// <summary>The act: empties the item as the person of the session, with the motive and the protocol of the form.</summary>
    internal static async Task<IResult> EmptyAsync(HttpContext context, [FromServices] ItemEmptyingDesk? desk)
    {
        if (desk is null)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var domain = await EmptyingScreensEndpoint.ActiveAsync(context, desk);
        var entered = context.RequestServices.GetRequiredService<SessionStore>().Resolve(SessionCookie.Read(context.Request));
        if (domain is null || entered is not { } identifier)
        {
            return ScreenResult.NotFound(context);
        }

        var form = context.Request.HasFormContentType ? await context.Request.ReadFormAsync(context.RequestAborted) : null;
        var typed = form?[EmptyingScreen.ItemField].ToString() ?? string.Empty;
        return EmptyingScreensEndpoint.TryItem(typed, out var item)
            ? await ActAsync(context, (desk, domain, identifier), new Posted(item, form))
            : PageRedirect.Of(context, EmptyingScreensEndpoint.Back(typed));
    }

    private static async Task<IResult> ActAsync(HttpContext context, (ItemEmptyingDesk Desk, EmptyingScreensEndpoint.ActiveDomain Domain, long Identifier) who, Posted posted)
    {
        var given = posted.Form?[EmptyingConfirmScreen.MotiveField].ToString();
        var asked = new Asked(given, posted.Form?[EmptyingConfirmScreen.ReferenceField].ToString() ?? string.Empty, Problem.None);
        if (EmptyingMotives.Parse(given) is not { } motive)
        {
            return Confirmation(context, (who.Domain, posted.Item), asked with { Problem = Problem.MotiveMissing });
        }

        var request = new ItemEmptyingDesk.EmptyingRequest(who.Domain.Id, posted.Item, motive, asked.Reference!);
        var result = await who.Desk.EmptyAsync(who.Identifier, request, context.RequestAborted);
        return Answered(context, result.Outcome, (who.Domain, posted.Item), asked);
    }

    /// <summary>
    /// What the act ended in: the page of the item for an emptying or one already done, the form of the entry for an item that is not there,
    /// and the confirmation again for what the form got wrong.
    /// </summary>
    private static IResult Answered(HttpContext context, EmptyingOutcome outcome, (EmptyingScreensEndpoint.ActiveDomain Domain, long Item) at, Asked asked)
    {
        var entry = EmptyingScreensEndpoint.Back(at.Item);
        return outcome switch
        {
            EmptyingOutcome.Emptied => PageRedirect.Of(context, $"{entry}&{EmptyingScreen.DoneField}=1"),
            EmptyingOutcome.AlreadyEmptied => PageRedirect.Of(context, entry),
            EmptyingOutcome.NotFound => EmptyingScreensEndpoint.Missing(context, at.Domain, at.Item),
            EmptyingOutcome.ReferenceRefused => Confirmation(context, at, asked with { Problem = Problem.ReferenceRefused }),
            EmptyingOutcome.NotRecorded => Confirmation(context, at, asked with { Problem = Problem.NotWritten }, StatusCodes.Status503ServiceUnavailable),
            EmptyingOutcome.AuthorWithoutFunction => ScreenResult.NotFound(context),
            _ => ScreenResult.NotFound(context),
        };
    }

    private static RazorComponentResult<ScreenFrame> Confirmation(
        HttpContext context, (EmptyingScreensEndpoint.ActiveDomain Domain, long Item) at, Asked asked, int status = StatusCodes.Status200OK)
    {
        var screen = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(EmptyingConfirmScreen.ItemId)] = at.Item,
            [nameof(EmptyingConfirmScreen.Domain)] = at.Domain.Name,
            [nameof(EmptyingConfirmScreen.Motive)] = asked.Motive,
            [nameof(EmptyingConfirmScreen.Reference)] = asked.Reference,
            [nameof(EmptyingConfirmScreen.MotiveMissing)] = asked.Problem == Problem.MotiveMissing,
            [nameof(EmptyingConfirmScreen.ReferenceRefused)] = asked.Problem == Problem.ReferenceRefused,
            [nameof(EmptyingConfirmScreen.NotWritten)] = asked.Problem == Problem.NotWritten,
        };
        var page = ScreenResult.Of(context, typeof(EmptyingConfirmScreen), EmptyingConfirmScreen.Title, screen);
        page.StatusCode = status;
        return page;
    }

    /// <summary>What went wrong on the form of the confirmation, given back with the page.</summary>
    private enum Problem
    {
        None,
        MotiveMissing,
        ReferenceRefused,
        NotWritten,
    }

    private sealed record Asked(string? Motive, string? Reference, Problem Problem);

    private sealed record Posted(long Item, IFormCollection? Form);
}
