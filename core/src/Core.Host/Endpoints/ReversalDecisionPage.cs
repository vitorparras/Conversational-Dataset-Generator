using System.Globalization;
using Core.Host.Screens;
using Core.Persistence.Content.Reading;
using Core.Persistence.Content.Reversal;
using Acting = Core.Host.Endpoints.ReversalScreensEndpoint.Acting;
using Shown = Core.Host.Screens.ReversalDecisionScreen.Shown;

namespace Core.Host.Endpoints;

/// <summary>The page of the decision on a request, with why it asks and what for when it is still to decide, and the day the authorization is offered until.</summary>
internal static class ReversalDecisionPage
{
    /// <summary>The day as the field of a date sends it and takes it.</summary>
    internal const string DayFormat = "yyyy-MM-dd";

    /// <summary>The page of the decision; a request still to decide opens its text first, and a text the chain did not record the reading of is not shown and the page says why.</summary>
    internal static async Task<IResult> OfAsync(HttpContext context, Acting acting, ListedReversal request, Showing asked)
    {
        ReversalJustification? text = null;
        var (showing, answered) = (asked.Shown, asked.Status);
        if (asked.Shown is Shown.Open or Shown.PastDay && request.State is ReversalState.Pending or ReversalState.Expired)
        {
            text = await acting.Access.Reader.ReadTextAsync(new ReversalAct(acting.Domain.Id, request.Id, acting.Actor), context.RequestAborted);
            if (text.Outcome != ContentReadOutcome.Read)
            {
                (text, showing, answered) = (null, Shown.NotWritten, StatusCodes.Status503ServiceUnavailable);
            }
        }

        var offered = DateOnly.FromDateTime(acting.Desk.Time.GetUtcNow().UtcDateTime).AddDays(request.TermDays).ToString("O", CultureInfo.InvariantCulture);
        var page = ScreenResult.Of(context, typeof(ReversalDecisionScreen), ReversalDecisionScreen.TitleOf(request.Id, showing), new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(ReversalDecisionScreen.Request)] = request,
            [nameof(ReversalDecisionScreen.Text)] = text,
            [nameof(ReversalDecisionScreen.Requester)] = await acting.Desk.NameOfAsync(request.RequestedBy, context.RequestAborted),
            [nameof(ReversalDecisionScreen.Until)] = asked.Until ?? offered,
            [nameof(ReversalDecisionScreen.Showing)] = showing,
        });
        page.StatusCode = answered;
        return page;
    }

    /// <summary>What the page is asked to show, the day typed when there is one, and the status it answers with.</summary>
    internal sealed record Showing(Shown Shown, string? Until = null, int Status = StatusCodes.Status200OK);
}
