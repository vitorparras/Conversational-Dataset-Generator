using Core.Host.Screens;
using Core.Persistence.Content.Reversal;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Shown = Core.Host.Screens.ReversalRevealScreen.Shown;

namespace Core.Host.Endpoints;

/// <summary>
/// The revelation of the marker of a request to who asked it (issues 105 and 616): the GET only confirms what will happen, on a page of its own, and leads a request not authorized back to
/// itself; the POST reveals, and the identity is answered once, with no cache, only after the revelation and its event are committed; a denial says why, and no one else opens the request.
/// </summary>
internal static class ReversalRevealEndpoint
{
    /// <summary>What each denial says; any other end but the identity revealed is a chain that did not record it.</summary>
    private static readonly Dictionary<ReversalOutcome, Shown> Denials = new()
    {
        [ReversalOutcome.Expired] = Shown.Expired,
        [ReversalOutcome.AlreadyRevealed] = Shown.Used,
        [ReversalOutcome.Emptied] = Shown.Emptied,
    };

    internal static Task<IResult> ConfirmAsync(HttpContext context, long id, [FromServices] ReversalDesk? desk) => ReversalScreensEndpoint.ActingAsync(context, desk, async acting =>
    {
        if (await acting.Access.Reader.ReadAsync(acting.Domain.Id, id, context.RequestAborted) is not { } request || request.RequestedBy != acting.Actor)
        {
            return ScreenResult.NotFound(context);
        }

        return request.State == ReversalState.Authorized ? PageOf(context, request, Shown.Confirm, null) : PageRedirect.Of(context, ScreenPaths.ReversalMineOf(id));
    });

    /// <summary>Reveals in the name of the person of the session; a request not authorized is led back to itself, and a chain that did not record the revelation leaves the page unavailable.</summary>
    internal static Task<IResult> RevealAsync(HttpContext context, long id, [FromServices] ReversalDesk? desk) => ReversalScreensEndpoint.ActingAsync(context, desk, async acting =>
    {
        var revealed = await acting.Access.Revelations.RevealAsync(new ReversalAct(acting.Domain.Id, id, acting.Actor), context.RequestAborted);
        if (revealed.Outcome == ReversalOutcome.Closed)
        {
            return PageRedirect.Of(context, ScreenPaths.ReversalMineOf(id));
        }

        if (revealed.Outcome == ReversalOutcome.NotFound || await acting.Access.Reader.ReadAsync(acting.Domain.Id, id, context.RequestAborted) is not { } request)
        {
            return ScreenResult.NotFound(context);
        }

        var shown = revealed.Outcome == ReversalOutcome.Recorded && revealed.Identity is not null ? Shown.Revealed : Denials.GetValueOrDefault(revealed.Outcome, Shown.NotWritten);
        var page = PageOf(context, request, shown, revealed);
        page.StatusCode = shown == Shown.NotWritten ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status200OK;
        return page;
    });

    private static RazorComponentResult<ScreenFrame> PageOf(HttpContext context, ListedReversal request, Shown shown, ReversalRevelation? revealed) =>
        ScreenResult.Of(context, typeof(ReversalRevealScreen), ReversalRevealScreen.TitleOf(request, shown), new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(ReversalRevealScreen.Request)] = request,
            [nameof(ReversalRevealScreen.Showing)] = shown,
            [nameof(ReversalRevealScreen.Identity)] = revealed?.Identity,
            [nameof(ReversalRevealScreen.RevealedAt)] = revealed?.RevealedAt,
        });
}
