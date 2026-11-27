using System.Collections.Frozen;
using Core.Host.Screens;
using Core.Persistence.Ingestion;
using Core.Persistence.People;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;

namespace Core.Host.Endpoints;

/// <summary>The routes of the review of the sample (#610), for the curation, none in the menu: draw, review item by item, finding that rejects the batch, approval.</summary>
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

    /// <summary>Every outcome of a step goes back to the review with the mark of what became of it in the address; an outcome this does not know is a defect, not a page.</summary>
    private static IResult Answered(HttpContext context, long id, BatchReviewOutcome outcome)
    {
        if (outcome is BatchReviewOutcome.NotRecorded or BatchReviewOutcome.NotFound)
        {
            return outcome == BatchReviewOutcome.NotFound ? ScreenResult.NotFound(context) : ScreenResult.Unavailable(context);
        }

        return Back(context, id, Marks[outcome]);
    }

    /// <summary>The mark of each outcome that goes back to the review; one missing from here is a defect that fails at once, not a page that says nothing.</summary>
    private static readonly FrozenDictionary<BatchReviewOutcome, string> Marks = new Dictionary<BatchReviewOutcome, string>
    {
        [BatchReviewOutcome.Drawn] = SampleReviewScreen.DrawnMark,
        [BatchReviewOutcome.Marked] = SampleReviewScreen.MarkedMark,
        [BatchReviewOutcome.Reviewed] = SampleReviewScreen.MarkedMark,
        [BatchReviewOutcome.Approved] = SampleReviewScreen.ApprovedMark,
        [BatchReviewOutcome.AlreadyReceived] = SampleReviewScreen.ApprovedMark,
        [BatchReviewOutcome.Rejected] = SampleReviewScreen.RejectedMark,
        [BatchReviewOutcome.AlreadySampled] = SampleReviewScreen.RepeatedMark,
        [BatchReviewOutcome.AlreadyMarked] = SampleReviewScreen.RepeatedMark,
        [BatchReviewOutcome.AlreadyReviewed] = SampleReviewScreen.RepeatedMark,
        [BatchReviewOutcome.NotOpen] = SampleReviewScreen.RefusedMark,
        [BatchReviewOutcome.NotSampled] = SampleReviewScreen.RefusedMark,
        [BatchReviewOutcome.SizeNotDeclared] = SampleReviewScreen.RefusedMark,
        [BatchReviewOutcome.FindingRefused] = SampleReviewScreen.RefusedMark,
        [BatchReviewOutcome.PlaceNotNext] = SampleReviewScreen.RefusedMark,
        [BatchReviewOutcome.ReviewIncomplete] = SampleReviewScreen.RefusedMark,
        [BatchReviewOutcome.NotPseudonymized] = SampleReviewScreen.NoTextMark,
    }.ToFrozenDictionary();

    private static IResult Back(HttpContext context, long id, string mark) => PageRedirect.Of(context, $"{ScreenPaths.BatchReviewOf(id)}?{SampleReviewScreen.ResultKey}={mark}");

    private static Task<IResult> DrawAsync(HttpContext context, long id, [AsParameters] SampleReviewSources sources) =>
        GuardedAsync(context, (id, sources), async (found, reviewer) =>
            Answered(context, id, (await sources.Desk!.DrawAsync(found.Batch, reviewer, context.RequestAborted).ConfigureAwait(false)).Outcome));

    private static Task<IResult> MarkAsync(
        HttpContext context, long id, int place, [AsParameters] SampleReviewSources sources) =>
        GuardedAsync(context, (id, sources), async (found, _) =>
            Answered(context, id, await sources.Desk!.MarkAsync(found.Batch, place, context.RequestAborted).ConfigureAwait(false)));

    private static Task<IResult> ApproveAsync(HttpContext context, long id, [AsParameters] SampleReviewSources sources) =>
        GuardedAsync(context, (id, sources), async (found, reviewer) =>
        {
            var outcome = await sources.Desk!.ApproveAsync(found.Batch, reviewer, context.RequestAborted).ConfigureAwait(false);
            return outcome == BatchReviewOutcome.AlreadyReceived ? await FirstOfAsync(context, id, found, sources).ConfigureAwait(false) : Answered(context, id, outcome);
        });

    /// <summary>The copy of a file already accepted was rejected as such, and the person is taken to the batch that holds the file, or to the copy when that one cannot be read.</summary>
    private static async Task<IResult> FirstOfAsync(HttpContext context, long id, (BatchEntry Batch, string Domain) found, SampleReviewSources sources)
    {
        var rejected = await sources.Register!.ReadAsync([found.Batch.DomainId], id, context.RequestAborted).ConfigureAwait(false);
        return PageRedirect.Of(context, rejected?.PreviousBatch is { } first ? ScreenPaths.BatchOf(first) : ScreenPaths.BatchOf(id));
    }

    private static Task<IResult> ReviewAsync(HttpContext context, long id, [AsParameters] SampleReviewSources sources) =>
        GuardedAsync(context, (id, sources), async (found, reviewer) =>
        {
            var (outcome, screen) = await sources.Desk!.ViewAsync(found.Batch, found.Domain, reviewer, context.RequestAborted).ConfigureAwait(false);
            if (outcome == BatchReviewOutcome.Drawn)
            {
                var counts = sources.Counts is { } reading ? await reading.ReadAsync(found.Batch.DomainId, id, context.RequestAborted).ConfigureAwait(false) : null;
                return ScreenResult.Of(context, typeof(SampleReviewScreen), SampleReviewScreen.Title, new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [nameof(SampleReviewScreen.Sample)] = screen,
                    [nameof(SampleReviewScreen.Version)] = counts?.SanitizationVersion,
                    [nameof(SampleReviewScreen.Action)] = ScreenPaths.BatchReviewOf(id),
                    [nameof(SampleReviewScreen.Result)] = context.Request.Query[SampleReviewScreen.ResultKey].ToString(),
                });
            }

            if (outcome == BatchReviewOutcome.NotFound)
            {
                return ScreenResult.NotFound(context);
            }

            return outcome == BatchReviewOutcome.NotSampled
                ? ScreenResult.Of(context, typeof(SampleDrawScreen), SampleDrawScreen.Title, DrawOf(found, sources.Desk.Declared))
                : ScreenResult.Unavailable(context);
        });

    private static Task<IResult> FindingAsync(
        HttpContext context, long id, int place, [AsParameters] SampleReviewSources sources) =>
        GuardedAsync(context, (id, sources), (_, _) => Task.FromResult<IResult>(RejectionScreen(context, id, place, (null, null))));

    private static Task<IResult> RejectAsync(
        HttpContext context, long id, int place, [AsParameters] SampleReviewSources sources) =>
        GuardedAsync(context, (id, sources), async (found, reviewer) =>
        {
            var kind = (await RequestReader.FormAsync(context).ConfigureAwait(false))?[SampleRejectScreen.FindingField].ToString();
            if (SampleTexts.Findings.All(option => option.Value != kind))
            {
                return RejectionScreen(context, id, place, (kind, "Escolha o que foi achado."));
            }

            return Answered(context, id, await sources.Desk!.RejectAsync(found.Batch, (place, kind!, reviewer), context.RequestAborted).ConfigureAwait(false));
        });

    private static RazorComponentResult<ScreenFrame> RejectionScreen(HttpContext context, long id, int place, (string? Finding, string? Error) shown) =>
        ScreenResult.Of(context, typeof(SampleRejectScreen), SampleRejectScreen.Title, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(SampleRejectScreen.Batch)] = id,
            [nameof(SampleRejectScreen.Position)] = place,
            [nameof(SampleRejectScreen.Action)] = ScreenPaths.BatchFindingOf(id, place),
            [nameof(SampleRejectScreen.Back)] = ScreenPaths.BatchReviewOf(id),
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
            [nameof(SampleDrawScreen.Action)] = ScreenPaths.BatchDrawOf(found.Batch.Id),
            [nameof(SampleDrawScreen.Back)] = ScreenPaths.Batches,
        };

    /// <summary>Runs a step for the batch the person may see: one outside the grants is not found, and a database that does not answer leaves the page unavailable, with nothing written.</summary>
    private static async Task<IResult> GuardedAsync(
        HttpContext context, (long Id, SampleReviewSources Sources) request, Func<(BatchEntry Batch, string Domain), long, Task<IResult>> step)
    {
        var services = request.Sources;
        if (services.Desk is null || services.Register is null || services.Listing is null || RequestReader.SessionOf(context) is not { } reviewer)
        {
            return ScreenResult.Unavailable(context);
        }

        try
        {
            var granted = await GrantedDomains.OfAsync(services.Listing, (context.Features.Get<ScreenReach>() ?? ScreenReach.Nothing).Granted, context.RequestAborted).ConfigureAwait(false);
            return await services.Register.ReadAsync(granted.Keys, request.Id, context.RequestAborted).ConfigureAwait(false) is { } batch
                ? await step((batch, granted[batch.DomainId]), reviewer).ConfigureAwait(false)
                : ScreenResult.NotFound(context);
        }
        catch (Exception unreadable) when (unreadable is NpgsqlException or InvalidOperationException or TimeoutException)
        {
            return ScreenResult.Unavailable(context);
        }
    }
}
