using System.Globalization;
using Core.Host.Operators;
using Core.Host.Screens;
using Core.Persistence.Domains;
using Core.Persistence.Ingestion;
using Core.Persistence.People;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>
/// The screen of the batches (#608), for the curation: the batches of the domains granted, whatever their state, with the filter and the page in the address and both cut by the server
/// from the register of the submissions (#75). With no domain granted the list is empty and says why (CA-3), and the menu does not list it. Nothing of the material is read.
/// </summary>
internal static class BatchScreensEndpoint
{
    private const int BatchesPlace = 20;

    internal static void Map(IEndpointRouteBuilder endpoints) =>
        _ = endpoints.MapGet(ScreenPaths.Batches, ListAsync).WithMetadata(ScreenAccess.OfFunction(BatchesScreen.Title, BatchesPlace, PersonFunctions.Curation));

    /// <summary>With no database declared there is no batch to list, and the page says it is the host that cannot, as the other screens do.</summary>
    private static async Task<IResult> ListAsync(
        HttpContext context, [FromServices] BatchRegister? register, [FromServices] DomainListing? listing, [FromServices] OperatorAdministration? staff)
    {
        if (register is null || listing is null || staff is null)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var query = context.Request.Query;
        var (state, from, until) = (query[BatchesScreen.StateKey].ToString(), query[BatchesScreen.FromKey].ToString(), query[BatchesScreen.UntilKey].ToString());
        var (since, before, fromError, untilError) = BatchesScreen.PeriodOf(from, until);
        var page = Math.Clamp(int.TryParse(query[BatchesScreen.PageKey], NumberStyles.None, CultureInfo.InvariantCulture, out var asked) ? asked : 1, 1, BatchListing.LastPage);
        var granted = (context.Features.Get<ScreenReach>() ?? ScreenReach.Nothing).Granted;
        var (rows, any, next) = await BatchListing.PageAsync((register, listing, staff.Registry), granted, (BatchesScreen.StateOf(state), since, before, page), context.RequestAborted);
        var screen = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(BatchesScreen.Rows)] = rows,
            [nameof(BatchesScreen.Granted)] = any,
            [nameof(BatchesScreen.State)] = state,
            [nameof(BatchesScreen.From)] = from,
            [nameof(BatchesScreen.Until)] = until,
            [nameof(BatchesScreen.FromError)] = fromError,
            [nameof(BatchesScreen.UntilError)] = untilError,
            [nameof(BatchesScreen.Page)] = page,
            [nameof(BatchesScreen.HasNext)] = next,
        };
        return ScreenResult.Of(context, typeof(BatchesScreen), BatchesScreen.Title, screen);
    }
}
