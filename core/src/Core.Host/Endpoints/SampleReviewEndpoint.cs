using System.Globalization;
using Core.Host.Review;
using Core.Host.Screens;
using Core.Persistence.Domains;
using Core.Persistence.Ingestion;
using Core.Persistence.People;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Core.Host.Endpoints;

/// <summary>The routes of the review of the sample (#610), for the curation and none in the menu: the draw, the review item by item, the finding that rejects the batch and the approval. A batch outside the grants is not found, a step is answered by sending the person back to the review, and a step the database could not take is unavailable.</summary>
internal static class SampleReviewEndpoint
{
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        var access = ScreenAccess.Unlisted(PersonFunctions.Curation);
        _ = endpoints.MapPost(ScreenPaths.BatchDraw, DrawAsync).WithMetadata(access);
        _ = endpoints.MapGet(ScreenPaths.BatchReview, ReviewAsync).WithMetadata(access);
        _ = endpoints.MapPost(ScreenPaths.BatchReviewMark, MarkAsync).WithMetadata(access);
        _ = endpoints.MapPost(ScreenPaths.BatchReviewApprove, ApproveAsync).WithMetadata(access);
        _ = endpoints.MapGet(ScreenPaths.BatchReviewFinding, FindingAsync).WithMetadata(access);
        _ = endpoints.MapPost(ScreenPaths.BatchReviewFinding, RejectAsync).WithMetadata(access);
    }

    private static string ReviewOf(long id) => $"{ScreenPaths.Batches}/{id.ToString(CultureInfo.InvariantCulture)}{ScreenPaths.ReviewSuffix}";

    private static IResult Answered(HttpContext context, long id, BatchReviewOutcome outcome) =>
        outcome == BatchReviewOutcome.NotRecorded ? ScreenResult.Unavailable(context) : PageRedirect.Of(context, ReviewOf(id));

    private static Task<IResult> DrawAsync(HttpContext context, long id, [FromServices] SampleReviewDesk? desk, [FromServices] BatchRegister? register, [FromServices] DomainListing? listing) =>
        GuardedAsync(context, (desk, register, listing), async (found, person) =>
            Answered(context, id, (await desk!.DrawAsync(found.Batch, person, context.RequestAborted).ConfigureAwait(false)).Outcome));

    private static Task<IResult> MarkAsync(
        HttpContext context, long id, int place, [FromServices] SampleReviewDesk? desk, [FromServices] BatchRegister? register, [FromServices] DomainListing? listing) =>
        GuardedAsync(context, (desk, register, listing), async (found, _) =>
            Answered(context, id, await desk!.MarkAsync(found.Batch, place, context.RequestAborted).ConfigureAwait(false)));

    private static Task<IResult> ApproveAsync(HttpContext context, long id, [FromServices] SampleReviewDesk? desk, [FromServices] BatchRegister? register, [FromServices] DomainListing? listing) =>
        GuardedAsync(context, (desk, register, listing), async (found, person) =>
            Answered(context, id, await desk!.ApproveAsync(found.Batch, person, context.RequestAborted).ConfigureAwait(false)));

    private static Task<IResult> ReviewAsync(HttpContext context, long id, [FromServices] SampleReviewDesk? desk, [FromServices] BatchRegister? register, [FromServices] DomainListing? listing) =>
        GuardedAsync(context, (desk, register, listing), async (found, person) =>
        {
            var (outcome, screen) = await desk!.ViewAsync(found.Batch, found.Domain, person, context.RequestAborted).ConfigureAwait(false);
            if (outcome == BatchReviewOutcome.Drawn)
            {
                return ScreenResult.Of(context, typeof(SampleReviewScreen), SampleReviewScreen.Title,
                    new Dictionary<string, object?>(StringComparer.Ordinal) { [nameof(SampleReviewScreen.Sample)] = screen, [nameof(SampleReviewScreen.Action)] = ReviewOf(id) });
            }

            return outcome == BatchReviewOutcome.NotSampled
                ? ScreenResult.Of(context, typeof(SampleDrawScreen), SampleDrawScreen.Title, DrawOf(found, desk.Declared))
                : ScreenResult.Unavailable(context);
        });

    private static Task<IResult> FindingAsync(
        HttpContext context, long id, int place, [FromServices] SampleReviewDesk? desk, [FromServices] BatchRegister? register, [FromServices] DomainListing? listing) =>
        GuardedAsync(context, (desk, register, listing), (_, _) => Task.FromResult<IResult>(RejectionScreen(context, id, place, (null, null))));

    private static Task<IResult> RejectAsync(
        HttpContext context, long id, int place, [FromServices] SampleReviewDesk? desk, [FromServices] BatchRegister? register, [FromServices] DomainListing? listing) =>
        GuardedAsync(context, (desk, register, listing), async (found, person) =>
        {
            var kind = (await RequestReader.FormAsync(context).ConfigureAwait(false))?[SampleRejectScreen.FindingField].ToString();
            if (SampleTexts.Findings.All(option => option.Value != kind))
            {
                return RejectionScreen(context, id, place, (kind, "Escolha o que foi achado."));
            }

            return Answered(context, id, await desk!.RejectAsync(found.Batch, (place, kind!, person), context.RequestAborted).ConfigureAwait(false));
        });

    private static RazorComponentResult<ScreenFrame> RejectionScreen(HttpContext context, long id, int place, (string? Finding, string? Error) shown) =>
        ScreenResult.Of(context, typeof(SampleRejectScreen), SampleRejectScreen.Title, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(SampleRejectScreen.Batch)] = id,
            [nameof(SampleRejectScreen.Position)] = place,
            [nameof(SampleRejectScreen.Action)] = $"{ReviewOf(id)}/itens/{place.ToString(CultureInfo.InvariantCulture)}/achado",
            [nameof(SampleRejectScreen.Back)] = ReviewOf(id),
            [nameof(SampleRejectScreen.Finding)] = shown.Finding,
            [nameof(SampleRejectScreen.Error)] = shown.Error,
        });

    private static Dictionary<string, object?> DrawOf((BatchEntry Batch, string Domain) found, int? declared) =>
        new(StringComparer.Ordinal)
        {
            [nameof(SampleDrawScreen.Batch)] = found.Batch.Id,
            [nameof(SampleDrawScreen.Domain)] = found.Domain,
            [nameof(SampleDrawScreen.Declared)] = declared,
            [nameof(SampleDrawScreen.Open)] = found.Batch.State == BatchState.Open,
            [nameof(SampleDrawScreen.Action)] = $"{ScreenPaths.Batches}/{found.Batch.Id.ToString(CultureInfo.InvariantCulture)}/amostra",
            [nameof(SampleDrawScreen.Back)] = ScreenPaths.Batches,
        };

    /// <summary>Runs a step for the batch the person may see: one outside the grants is not found, and a database that does not answer leaves the page unavailable, with nothing written.</summary>
    private static async Task<IResult> GuardedAsync(
        HttpContext context, (SampleReviewDesk? Desk, BatchRegister? Register, DomainListing? Listing) services, Func<(BatchEntry Batch, string Domain), long, Task<IResult>> step)
    {
        if (services.Desk is null || services.Register is null || services.Listing is null || RequestReader.SessionOf(context) is not { } person)
        {
            return ScreenResult.Unavailable(context);
        }

        try
        {
            var granted = await GrantedDomains.OfAsync(services.Listing, (context.Features.Get<ScreenReach>() ?? ScreenReach.Nothing).Granted, context.RequestAborted).ConfigureAwait(false);
            var id = long.Parse((string)context.Request.RouteValues["id"]!, CultureInfo.InvariantCulture);
            return await services.Register.ReadAsync(granted.Keys, id, context.RequestAborted).ConfigureAwait(false) is { } batch
                ? await step((batch, granted[batch.DomainId]), person).ConfigureAwait(false)
                : ScreenResult.NotFound(context);
        }
        catch (Exception unreadable) when (unreadable is NpgsqlException or InvalidOperationException or TimeoutException)
        {
            return ScreenResult.Unavailable(context);
        }
    }
}
