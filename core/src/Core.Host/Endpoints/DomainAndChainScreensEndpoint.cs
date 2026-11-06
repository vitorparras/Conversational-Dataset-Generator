using Core.Host.Screens;
using Core.Persistence.Domains;
using Core.Persistence.People;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>
/// The screens of the domains (#602) and of the chain (#605), which show what the routes of JSON read, from the same calls. The domains are reached by the technical administration and,
/// with a domain granted, by the curation; the chain by the technical administration and the audit. A verification asked for now is confirmed on a page of its own (decision 0051),
/// posted from it, and recorded with who asked; nothing here rebuilds a link.
/// </summary>
internal static class DomainAndChainScreensEndpoint
{
    private const int DomainsPlace = 92;
    private const int ChainPlace = 93;

    /// <summary>The mark in the address of the chain that a verification asked for has just finished.</summary>
    private const string AskedMark = "pedida";

    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        var chain = new[] { PersonFunctions.Administration, PersonFunctions.Audit };
        _ = endpoints.MapGet(ScreenPaths.Domains, DomainsAsync)
            .WithMetadata(ScreenAccess.Of(DomainsScreen.Title, DomainsPlace, PersonFunctions.Administration, PersonFunctions.Curation));
        _ = endpoints.MapGet(ScreenPaths.Chain, ChainAsync).WithMetadata(ScreenAccess.Of(ChainScreen.Title, ChainPlace, chain));
        _ = endpoints.MapGet(ScreenPaths.ChainCheck, Confirm).WithMetadata(ScreenAccess.Unlisted(chain));
        _ = endpoints.MapPost(ScreenPaths.ChainCheck, CheckAsync).WithMetadata(ScreenAccess.Unlisted(chain));
    }

    /// <summary>With no database declared there is no domain to list, and the page says it is the host that cannot, as the route of JSON does.</summary>
    private static async Task<IResult> DomainsAsync(HttpContext context, [FromServices] DomainListing? listing)
    {
        if (listing is null)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var screen = new Dictionary<string, object?>(StringComparer.Ordinal) { [nameof(DomainsScreen.Domains)] = await listing.ListAsync(context.RequestAborted) };
        return ScreenResult.Of(context, typeof(DomainsScreen), DomainsScreen.Title, screen);
    }

    /// <summary>A chain that cannot be read now is a page that says so, with 503, and shows nothing of a verification.</summary>
    private static async Task<RazorComponentResult<ScreenFrame>> ChainAsync(HttpContext context, [AsParameters] ChainSources sources)
    {
        if (await ChainState.ReadAsync(sources, context.RequestAborted) is not { } shown)
        {
            var unreadable = ScreenResult.Of(context, typeof(ChainScreen), ChainScreen.Title, new Dictionary<string, object?>(StringComparer.Ordinal) { [nameof(ChainScreen.Unreadable)] = true });
            unreadable.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return unreadable;
        }

        var screen = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(ChainScreen.Last)] = shown.Last,
            [nameof(ChainScreen.History)] = shown.History,
            [nameof(ChainScreen.Interval)] = shown.Interval,
            [nameof(ChainScreen.Overdue)] = shown.Overdue,
            [nameof(ChainScreen.Protection)] = shown.Protection,
            [nameof(ChainScreen.LastSeal)] = shown.LastSeal,
            [nameof(ChainScreen.SealWarning)] = shown.Warning,
            [nameof(ChainScreen.Asked)] = context.Request.Query.ContainsKey(AskedMark),
        };
        return ScreenResult.Of(context, typeof(ChainScreen), ChainScreen.Title, screen);
    }

    private static RazorComponentResult<ScreenFrame> Confirm(HttpContext context) => ScreenResult.Of(context, typeof(ChainCheckScreen), ChainCheckScreen.Title);

    /// <summary>A verification done takes the browser to the chain, which shows it; one refused for coming too soon, or that did not reach its end, is the confirmation again saying so.</summary>
    private static async Task<IResult> CheckAsync(HttpContext context, [AsParameters] ChainSources sources)
    {
        var asked = await ChainState.VerifyAsync(sources, SessionCookie.Read(context.Request), seal: null, context.RequestAborted);
        if (asked.Done is not null)
        {
            return PageRedirect.Of(context, $"{ScreenPaths.Chain}?{AskedMark}");
        }

        var screen = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(ChainCheckScreen.WaitSeconds)] = asked.WaitSeconds,
            [nameof(ChainCheckScreen.Unfinished)] = asked.WaitSeconds is null,
        };
        return ScreenResult.Of(context, typeof(ChainCheckScreen), ChainCheckScreen.Title, screen);
    }
}
