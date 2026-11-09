using Core.Host.Screens;
using Core.Persistence.Domains;
using Core.Persistence.People;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>
/// The screen of the domains (#602), which reads the listing of the domains and changes nothing. It is reached by the technical administration and, with a domain granted,
/// by the curation, with an entry in the menu. When the last verification of the chain failed it says the writing is suspended (policy 005), and nothing of the link or the content.
/// </summary>
internal static class DomainScreensEndpoint
{
    private const int DomainsPlace = 92;

    internal static void Map(IEndpointRouteBuilder endpoints) =>
        _ = endpoints.MapGet(ScreenPaths.Domains, DomainsAsync)
            .WithMetadata(ScreenAccess.Of(DomainsScreen.Title, DomainsPlace, PersonFunctions.Administration, PersonFunctions.Curation));

    /// <summary>With no database declared there is no domain to list, and the page says it is the host that cannot, as the routes of JSON do.</summary>
    private static async Task<IResult> DomainsAsync(HttpContext context, [FromServices] DomainListing? listing, [FromServices] ChainVerificationService? verification)
    {
        if (listing is null)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var last = verification?.Reader is { } reader ? await reader.LastAsync(context.RequestAborted) : null;
        var screen = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(DomainsScreen.Domains)] = await listing.ListAsync(context.RequestAborted),
            [nameof(DomainsScreen.WritesSuspended)] = last is { Passed: false },
        };
        return ScreenResult.Of(context, typeof(DomainsScreen), DomainsScreen.Title, screen);
    }
}
