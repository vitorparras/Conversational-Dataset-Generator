using System.Globalization;
using Core.Host.Screens;
using Core.Persistence.Content.Reading;
using Core.Persistence.Content.Reversal;
using Microsoft.AspNetCore.Mvc;
using Shown = Core.Host.Screens.ReversalDecisionScreen.Shown;

namespace Core.Host.Endpoints;

/// <summary>
/// The decision on a request of another person of the curation of the domain: the page opens why it asks and what for only after the chain recorded the reading, and the POST authorizes until
/// the end of the day chosen, in universal time, or declines, and leads with a 303 to the confirmation of that request; whoever asked is led to the request as it follows it, and a POST of its own
/// is refused by the requests and said on the page.
/// </summary>
internal static partial class ReversalScreensEndpoint
{
    /// <summary>The marks in the address of a request just decided, which the page confirms only when the request stands so.</summary>
    internal const string Authorized = "autorizado";

    internal const string Declined = "recusado";

    /// <summary>The fields of the decision: what is decided, and the day, as the field of a date sends it, the authorization is valid until.</summary>
    internal const string DecisionField = "decisao";

    internal const string UntilField = "validade";

    internal const string Authorize = "autorizar";

    internal const string Decline = "recusar";

    internal const string DayFormat = "yyyy-MM-dd";

    /// <summary>The last second of the day chosen; a fraction past it would be rounded by the database into the day after.</summary>
    private static readonly TimeOnly LastSecond = new(23, 59, 59);

    private static async Task<IResult> DecisionAsync(HttpContext context, long id, [FromServices] ReversalDesk? desk)
    {
        if (desk is null)
        {
            return ScreenResult.Unavailable(context);
        }

        if (await ActorAsync(context, desk) is not { } actor)
        {
            return ScreenResult.NotFound(context);
        }

        using var access = await desk.OpenAsync(context.RequestAborted);
        if (access is null)
        {
            return ScreenResult.Unavailable(context);
        }

        if (await access.Reader.ReadAsync(actor.Domain.Id, id, context.RequestAborted) is not { } request)
        {
            return ScreenResult.NotFound(context);
        }

        if (request.RequestedBy == actor.Person)
        {
            return PageRedirect.Of(context, ScreenPaths.ReversalMineOf(id));
        }

        var query = context.Request.Query;
        var shown = (request.State, query.ContainsKey(Authorized), query.ContainsKey(Declined)) switch
        {
            (ReversalState.Authorized, true, _) => Shown.Authorized,
            (ReversalState.Declined, _, true) => Shown.Declined,
            _ => Shown.Open,
        };
        return await DecisionPageAsync(context, new Deciding(desk, access, actor), request, shown);
    }

    /// <summary>Authorizes until the day chosen, or declines, in the name of the person of the session; a day that is not one, or is past, comes back with what to fix and nothing authorized.</summary>
    private static async Task<IResult> DecideAsync(HttpContext context, long id, [FromServices] ReversalDesk? desk)
    {
        if (desk is null)
        {
            return ScreenResult.Unavailable(context);
        }

        if (await ActorAsync(context, desk) is not { } actor)
        {
            return ScreenResult.NotFound(context);
        }

        using var access = await desk.OpenAsync(context.RequestAborted);
        if (access is null)
        {
            return ScreenResult.Unavailable(context);
        }

        if (await access.Reader.ReadAsync(actor.Domain.Id, id, context.RequestAborted) is not { } request)
        {
            return ScreenResult.NotFound(context);
        }

        var form = await RequestReader.FormAsync(context);
        var (decision, until) = (form?[DecisionField].ToString(), form?[UntilField].ToString() ?? string.Empty);
        var at = new Deciding(desk, access, actor);
        var act = new ReversalAct(actor.Domain.Id, id, actor.Person);
        var declining = string.Equals(decision, Decline, StringComparison.Ordinal);
        ReversalResult result;
        if (declining)
        {
            result = await access.Requests.DeclineAsync(act, context.RequestAborted);
        }
        else if (string.Equals(decision, Authorize, StringComparison.Ordinal)
                 && DateOnly.TryParseExact(until, DayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            result = await access.Requests.AuthorizeAsync(act, new DateTimeOffset(day.ToDateTime(LastSecond), TimeSpan.Zero), context.RequestAborted);
        }
        else
        {
            return await DecisionPageAsync(context, at, request, Shown.PastDay, until);
        }

        return result.Outcome switch
        {
            ReversalOutcome.Recorded => PageRedirect.Of(context, $"{ScreenPaths.ReversalDecideOf(id)}?{(declining ? Declined : Authorized)}"),
            ReversalOutcome.OwnRequest => await DecisionPageAsync(context, at, request, Shown.Own),
            ReversalOutcome.TermInThePast => await DecisionPageAsync(context, at, request, Shown.PastDay, until),
            ReversalOutcome.Closed => await DecisionPageAsync(context, at, await access.Reader.ReadAsync(actor.Domain.Id, id, context.RequestAborted) ?? request, Shown.Open),
            ReversalOutcome.NotRecorded => await DecisionPageAsync(context, at, request, Shown.NotWritten, until, StatusCodes.Status503ServiceUnavailable),

            // The gate lets only the curation through; one whose function changed since answers as a route that is not there.
            _ => ScreenResult.NotFound(context),
        };
    }

    /// <summary>The page of the decision; a request still to decide opens its text first, and a text the chain did not record the reading of is not shown and the page says why.</summary>
    private static async Task<IResult> DecisionPageAsync(
        HttpContext context, Deciding at, ListedReversal request, Shown shown, string? until = null, int status = StatusCodes.Status200OK)
    {
        ReversalJustification? text = null;
        if (shown is Shown.Open or Shown.PastDay && request.State is ReversalState.Pending or ReversalState.Expired)
        {
            text = await at.Access.Reader.ReadTextAsync(new ReversalAct(at.Actor.Domain.Id, request.Id, at.Actor.Person), context.RequestAborted);
            if (text.Outcome != ContentReadOutcome.Read)
            {
                (text, shown, status) = (null, Shown.NotWritten, StatusCodes.Status503ServiceUnavailable);
            }
        }

        var offered = DateOnly.FromDateTime(at.Desk.Time.GetUtcNow().UtcDateTime).AddDays(request.TermDays).ToString(DayFormat, CultureInfo.InvariantCulture);
        var page = ScreenResult.Of(context, typeof(ReversalDecisionScreen), ReversalDecisionScreen.TitleOf(request.Id, shown), new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(ReversalDecisionScreen.Request)] = request,
            [nameof(ReversalDecisionScreen.Text)] = text,
            [nameof(ReversalDecisionScreen.Requester)] = await at.Desk.NameOfAsync(request.RequestedBy, context.RequestAborted),
            [nameof(ReversalDecisionScreen.Until)] = until ?? offered,
            [nameof(ReversalDecisionScreen.Showing)] = shown,
        });
        page.StatusCode = status;
        return page;
    }

    /// <summary>What a decision is taken over: the desk, the requests opened with the key read now, and who decides where.</summary>
    private sealed record Deciding(ReversalDesk Desk, ReversalAccess Access, Actor Actor);
}
