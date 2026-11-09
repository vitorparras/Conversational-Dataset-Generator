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
    private const string Removed = "Pessoa removida";

    /// <summary>The last page the address may ask for: past it the register would skip more than it reads for each domain, so the page stops there and says there is no next.</summary>
    private static readonly int LastPage = BatchRegister.MostSkipped / BatchesScreen.Size;

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
        var period = BatchesScreen.PeriodOf(from, until);
        var page = Math.Clamp(int.TryParse(query[BatchesScreen.PageKey], NumberStyles.None, CultureInfo.InvariantCulture, out var asked) ? asked : 1, 1, LastPage);
        var reach = context.Features.Get<ScreenReach>() ?? ScreenReach.Nothing;
        var domains = await GrantedDomains.OfAsync(listing, reach.Granted, context.RequestAborted);
        var filter = new BatchFilter
        {
            Domains = domains.Keys.ToList(),
            State = BatchesScreen.StateOf(state),
            From = period.From,
            Until = period.Until,
            Skip = (page - 1) * BatchesScreen.Size,
            Take = BatchesScreen.Size + 1,
        };
        IReadOnlyList<BatchEntry> found = domains.Count == 0 ? Array.Empty<BatchEntry>() : await register.ListAsync(filter, context.RequestAborted);
        var names = await staff.Registry.NamesOfAsync(found.Select(entry => entry.SubmittedBy), context.RequestAborted);
        var screen = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(BatchesScreen.Rows)] = found.Take(BatchesScreen.Size).Select(entry => RowOf(entry, domains, names)).ToList(),
            [nameof(BatchesScreen.Granted)] = domains.Count > 0,
            [nameof(BatchesScreen.State)] = state,
            [nameof(BatchesScreen.From)] = from,
            [nameof(BatchesScreen.Until)] = until,
            [nameof(BatchesScreen.FromError)] = period.FromError,
            [nameof(BatchesScreen.UntilError)] = period.UntilError,
            [nameof(BatchesScreen.Page)] = page,
            [nameof(BatchesScreen.HasNext)] = found.Count > BatchesScreen.Size && page < LastPage,
        };
        return ScreenResult.Of(context, typeof(BatchesScreen), BatchesScreen.Title, screen);
    }

    /// <summary>One batch as the list shows it: the domain and the person by name, a person the registry no longer has said as removed and never by the identifier.</summary>
    private static BatchesScreen.Row RowOf(BatchEntry entry, IReadOnlyDictionary<int, string> domains, IReadOnlyDictionary<long, string> names) => new(
        entry.Id,
        domains[entry.DomainId],
        entry.State,
        entry.Reason,
        names.TryGetValue(entry.SubmittedBy, out var name) ? name : Removed,
        entry.SubmittedAt,
        entry.Receipt is { } receipt ? new BatchesScreen.Receipt(receipt.Bytes, receipt.Digest, receipt.Warning) : null);
}
