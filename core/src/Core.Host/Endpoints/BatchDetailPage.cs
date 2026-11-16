using Core.Host.Operators;
using Core.Host.Screens;
using Core.Persistence.Domains;
using Core.Persistence.Ingestion;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>
/// The detail of one batch (#609), for the curation: the register says who sent it, when and in which state, and the database counts the items by form, the passages by destination, the replicas
/// and the losses of an accommodation, numbers and codes alone. A batch of a domain not granted is a batch that does not exist (policy 006). Nothing of the material is read.
/// </summary>
internal static class BatchDetailPage
{
    private const string Removed = "Pessoa removida";

    /// <summary>With no database declared there is no batch to show, and the page says it is the host that cannot, as the other screens do.</summary>
    internal static async Task<IResult> DetailAsync(
        long id, HttpContext context, [FromServices] BatchRegister? register, [FromServices] BatchCounts? counts, [FromServices] DomainListing? listing, [FromServices] OperatorAdministration? staff)
    {
        if (register is null || counts is null || listing is null || staff is null)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var granted = await GrantedDomains.OfAsync(listing, (context.Features.Get<ScreenReach>() ?? ScreenReach.Nothing).Granted, context.RequestAborted);
        if (await register.ReadAsync(granted.Keys, id, context.RequestAborted) is not { } entry)
        {
            return Results.NotFound();
        }

        var numbers = await counts.ReadAsync(entry.DomainId, entry.Id, context.RequestAborted);
        var names = await staff.Registry.NamesOfAsync(Enumerable.Repeat(entry.SubmittedBy, 1), context.RequestAborted);
        var who = names.TryGetValue(entry.SubmittedBy, out var name) ? name : Removed;
        var screen = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(BatchDetailScreen.Batch)] = DetailOf(entry, (granted[entry.DomainId], who), numbers),
            [nameof(BatchDetailScreen.ListAddress)] = ScreenPaths.Batches,
        };
        return ScreenResult.Of(context, typeof(BatchDetailScreen), BatchDetailScreen.Title, screen);
    }

    private static BatchDetailScreen.Detail DetailOf(BatchEntry entry, (string Domain, string SubmittedBy) named, BatchDetailCounts numbers)
    {
        var forms = numbers.Forms.Select(count => new BatchDetailScreen.FormCount(count.Code, int.CreateSaturating(count.Value))).ToList();
        var destinations = numbers.Destinations.Select(count => new BatchDetailScreen.DestinationCount(count.Code, int.CreateSaturating(count.Value))).ToList();
        return new BatchDetailScreen.Detail(entry.Id, named.Domain, entry.State, entry.Reason, named.SubmittedBy, entry.SubmittedAt, entry.OriginState == "declared")
        {
            Forms = forms,
            Destinations = destinations,
            Replicas = new BatchDetailScreen.ReplicaCount(int.CreateSaturating(numbers.Marked), int.CreateSaturating(numbers.Firsts)),
            Recognized = entry.PreviousBatch is { } previous ? new BatchDetailScreen.Recognition(previous, ScreenPaths.BatchOf(previous)) : null,
            Accommodation = AccommodationOf(entry.DeclaredForm, numbers.Accommodations),
            Warning = entry.Receipt?.Warning,
        };
    }

    /// <summary>
    /// The accommodation the items declared, each count keyed by the form received and the codes of the loss joined by a colon: the form of the first, the items of all and every loss
    /// said once; none when no item was accommodated.
    /// </summary>
    private static BatchDetailScreen.Accommodated? AccommodationOf(string? declared, IReadOnlyList<BatchCount> accommodations)
    {
        if (accommodations.Count == 0)
        {
            return null;
        }

        var into = accommodations[0].Code.Split(':')[0];
        var losses = accommodations.SelectMany(count => count.Code[(count.Code.IndexOf(':', StringComparison.Ordinal) + 1)..].Split(',', StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.Ordinal).ToList();
        return new BatchDetailScreen.Accommodated(declared ?? into, into, int.CreateSaturating(accommodations.Sum(count => count.Value)), losses);
    }
}
