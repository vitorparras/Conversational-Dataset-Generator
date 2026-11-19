using Core.Host.Operators;
using Core.Host.Screens;
using Core.Persistence.Domains;
using Core.Persistence.Ingestion;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>
/// The detail of one batch (#609), for the curation: the register says who sent it, when and in which state, and the database counts the items by form,
/// the passages by destination when any was counted, the replicas and the losses of an accommodation, numbers and codes alone.
/// A batch of a domain not granted is a batch that does not exist (policy 006). Nothing of the material is read.
/// </summary>
internal static class BatchDetailPage
{
    private const string Removed = "Pessoa removida";

    /// <summary>With no database declared there is no batch to show, and the page says it is the host that cannot, as the other screens do.</summary>
    internal static async Task<IResult> DetailAsync(long id, HttpContext context, [AsParameters] Sources sources)
    {
        if (sources is not { Register: { } register, Counts: { } counts, Listing: { } listing, Staff: { } staff })
        {
            return ScreenResult.Unavailable(context);
        }

        if (id <= 0)
        {
            return ScreenResult.NotFound(context);
        }

        var granted = await GrantedDomains.OfScreenAsync(listing, context);
        if (await register.ReadAsync(granted.Keys, id, context.RequestAborted) is not { } entry)
        {
            return ScreenResult.NotFound(context);
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

    /// <summary>What the detail reads, each from the services of the host, absent when no database was declared.</summary>
    internal sealed record Sources(
        [FromServices] BatchRegister? Register,
        [FromServices] BatchCounts? Counts,
        [FromServices] DomainListing? Listing,
        [FromServices] OperatorAdministration? Staff);

    private static BatchDetailScreen.Detail DetailOf(BatchEntry entry, (string Domain, string SubmittedBy) named, BatchDetailCounts numbers)
    {
        var forms = numbers.Forms.Select(count => new BatchDetailScreen.FormCount(count.Code, int.CreateSaturating(count.Value))).ToList();
        var destinations = numbers.Destinations.Sum(count => count.Value) == 0
            ? null
            : numbers.Destinations.Select(count => new BatchDetailScreen.DestinationCount(count.Code, int.CreateSaturating(count.Value))).ToList();
        return new BatchDetailScreen.Detail(entry.Id, named.Domain, entry.State, entry.Reason, named.SubmittedBy, entry.SubmittedAt, entry.OriginState == "declared")
        {
            Forms = forms,
            Destinations = destinations,
            Replicas = new BatchDetailScreen.ReplicaCount(int.CreateSaturating(numbers.Marked), int.CreateSaturating(numbers.Firsts)),
            Recognized = entry.PreviousBatch is { } previous ? new BatchDetailScreen.Recognition(previous, ScreenPaths.BatchOf(previous)) : null,
            Accommodation = AccommodationOf(entry.SourceForm, numbers.Accommodations),
            Warning = entry.Receipt?.Warning,
            StepsVersion = numbers.SanitizationVersion,
            Steps = [.. numbers.Steps.Select(step => new BatchDetailScreen.StepCount(step.Name, step.Edits, step.Items))],
        };
    }

    /// <summary>
    /// The accommodation the items declared, each count keyed by the form received and the codes of the loss joined by a colon: the form the format carries (or, for a batch
    /// opened before it was kept, the form of the first), the items of all and every loss said once; none when no item was accommodated.
    /// </summary>
    private static BatchDetailScreen.Accommodated? AccommodationOf(string? source, IReadOnlyList<BatchCount> accommodations)
    {
        if (accommodations.Count == 0)
        {
            return null;
        }

        var into = accommodations[0].Code.Split(':')[0];
        var losses = accommodations.SelectMany(count => count.Code[(count.Code.IndexOf(':', StringComparison.Ordinal) + 1)..].Split(',', StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.Ordinal).ToList();
        return new BatchDetailScreen.Accommodated(source ?? into, into, int.CreateSaturating(accommodations.Sum(count => count.Value)), losses);
    }
}
