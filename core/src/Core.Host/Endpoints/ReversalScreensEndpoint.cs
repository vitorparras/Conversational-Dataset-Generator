using Core.Host.Screens;
using Core.Persistence.People;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>
/// The screens of the request to reverse a marker and of its authorization (issues 614 and 615), reached only by the curation of the collection with a domain granted: the request, whose POST
/// either leads with a 303 to the request sent or comes back with what to fix, the requests of the person and each of them, and the requests of the others that wait for a decision, decided on
/// a page of their own. The requests decide what is refused and record it; the screens only say it, and no screen shows an identity.
/// </summary>
internal static class ReversalScreensEndpoint
{
    /// <summary>The mark in the address of a request just sent, which titles it so.</summary>
    internal const string Sent = "enviado";

    private const int AskPlace = 23;
    private const int AuthorizePlace = 24;
    private const int MinePlace = 25;

    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapGet(ScreenPaths.ReversalAsk, ReversalAskEndpoint.FormAsync).WithMetadata(ScreenAccess.Of(ReversalRequestScreen.Title, AskPlace, PersonFunctions.Curation));
        _ = endpoints.MapPost(ScreenPaths.ReversalAsk, ReversalAskEndpoint.AskAsync).WithMetadata(ScreenAccess.WithoutEntry(PersonFunctions.Curation));
        _ = endpoints.MapGet(ScreenPaths.ReversalAuthorize, (HttpContext context, [FromServices] ReversalDesk? desk) => ReversalListEndpoint.ListAsync(context, desk, mine: false))
            .WithMetadata(ScreenAccess.Of(ReversalListScreen.AwaitingTitle, AuthorizePlace, PersonFunctions.Curation));
        _ = endpoints.MapGet(ScreenPaths.ReversalDecide, ReversalDecisionEndpoint.DecisionAsync).WithMetadata(ScreenAccess.WithoutEntry(PersonFunctions.Curation));
        _ = endpoints.MapPost(ScreenPaths.ReversalDecide, ReversalDecisionEndpoint.DecideAsync).WithMetadata(ScreenAccess.WithoutEntry(PersonFunctions.Curation));
        _ = endpoints.MapGet(ScreenPaths.ReversalMine, (HttpContext context, [FromServices] ReversalDesk? desk) => ReversalListEndpoint.ListAsync(context, desk, mine: true))
            .WithMetadata(ScreenAccess.Of(ReversalListScreen.MineTitle, MinePlace, PersonFunctions.Curation));
        _ = endpoints.MapGet(ScreenPaths.ReversalMineOne, ReversalListEndpoint.FollowAsync).WithMetadata(ScreenAccess.WithoutEntry(PersonFunctions.Curation));
    }

    /// <summary>
    /// Runs a step in the name of the person of the session, over the requests opened with the key read now: no database or no key leaves the page unavailable, and no domain granted answers
    /// as a route that is not there.
    /// </summary>
    internal static async Task<IResult> ActingAsync(HttpContext context, ReversalDesk? desk, Func<Acting, Task<IResult>> step)
    {
        var granted = context.Features.Get<ScreenReach>()?.Granted.FirstOrDefault();
        if (desk is null)
        {
            return ScreenResult.Unavailable(context);
        }

        if (granted is null || RequestReader.SessionOf(context) is not { } actor || await desk.DomainIdOfAsync(granted, context.RequestAborted) is not { } id)
        {
            return ScreenResult.NotFound(context);
        }

        using var access = await desk.OpenAsync(context.RequestAborted);
        return access is null ? ScreenResult.Unavailable(context) : await step(new Acting(desk, access, new EmptyingScreensEndpoint.ActiveDomain(granted, id), actor));
    }

    /// <summary>What a step acts over: the desk, the requests opened with the key read now, the domain where the person of the session acts, and that person.</summary>
    internal sealed record Acting(ReversalDesk Desk, ReversalAccess Access, EmptyingScreensEndpoint.ActiveDomain Domain, long Actor);
}
