using Core.Host.Submissions;
using Core.Persistence.Ingestion;
using Core.Persistence.People;
using Core.Platform.Sessions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>
/// The routes of the ingestion, a shell over the service of the submissions: the gate, the submission of a file, the list of the batches and the detail of one. They are reached by the curation
/// with a domain granted, and whoever does not reach them gets the answer of a route that does not exist (policy 006); without a session the answer is 401, as in every route of JSON.
/// A domain the requester has no grant on is a domain that does not exist for them. With no database declared the host cannot, and says so.
/// </summary>
internal static class SubmissionsEndpoint
{
    internal const string SubmissionsRoute = "/ingestion/submissions";
    internal const string BatchesRoute = "/ingestion/batches";
    internal const string GateRoute = "/ingestion/gate";

    private const int BodyMargin = 64 * 1024;

    private static readonly ScreenAccess Access = ScreenAccess.Route(PersonFunctions.Curation);

    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapPost(SubmissionsRoute, SubmitAsync).WithMetadata(Access);
        _ = endpoints.MapGet(GateRoute, GateAsync).WithMetadata(Access);
        _ = endpoints.MapGet(BatchesRoute, ListAsync).WithMetadata(Access);
        _ = endpoints.MapGet(BatchesRoute + "/{id:long}", DetailAsync).WithMetadata(Access);
    }

    private static async Task<IResult> GateAsync([FromServices] SubmissionServiceHolder? services, HttpContext context)
    {
        if (services is null)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var service = await services.GetAsync(context.RequestAborted);
        var gate = await service.GateAsync(context.RequestAborted);
        return Results.Ok(new GateShown(gate.Open ? "open" : "blocked", gate.Code));
    }

    private static async Task<IResult> SubmitAsync([FromServices] SubmissionServiceHolder? services, [FromServices] IGrantedDomains? grants, HttpContext context)
    {
        if (services is null)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (SessionOf(context) is not { } author)
        {
            return Results.Unauthorized();
        }

        Bound(context, services.MaxBytes);
        if (await SubmissionForm.ReadAsync(context.Request, author, context.RequestAborted) is not { } read)
        {
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }

        var granted = await GrantedAsync(grants, author, context.RequestAborted);
        if (read.Request.DomainId > 0 && !granted.Contains(read.Request.DomainId))
        {
            return Results.NotFound();
        }

        var service = await services.GetAsync(context.RequestAborted);
        return read.HasFile ? SubmissionAnswer.Of(await service.SubmitAsync(read.Request, context.RequestAborted)) : SubmissionAnswer.NoFile(service.Check(read.Request));
    }

    private static async Task<IResult> ListAsync([FromServices] SubmissionServiceHolder? services, [FromServices] IGrantedDomains? grants, HttpContext context)
    {
        if (services is null)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (SessionOf(context) is not { } requester)
        {
            return Results.Unauthorized();
        }

        var granted = await GrantedAsync(grants, requester, context.RequestAborted);
        if (BatchQuery.Of(context.Request.Query, granted) is not { } asked)
        {
            return Results.Json(new FieldsRefused(BatchQuery.Problems(context.Request.Query)), statusCode: StatusCodes.Status400BadRequest);
        }

        var service = await services.GetAsync(context.RequestAborted);
        var entries = await service.ListAsync(asked.Filter, context.RequestAborted);
        return Results.Ok(new BatchesShown(asked.Page, asked.PageSize, [.. entries.Select(SubmissionAnswer.Shown)]));
    }

    private static async Task<IResult> DetailAsync(long id, [FromServices] SubmissionServiceHolder? services, [FromServices] IGrantedDomains? grants, HttpContext context)
    {
        if (services is null)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (SessionOf(context) is not { } requester)
        {
            return Results.Unauthorized();
        }

        var granted = await GrantedAsync(grants, requester, context.RequestAborted);
        var service = await services.GetAsync(context.RequestAborted);
        var entry = await service.DetailAsync(granted, id, context.RequestAborted);
        return entry is null ? Results.NotFound() : Results.Ok(new BatchDetailShown(SubmissionAnswer.Shown(entry)));
    }

    /// <summary>The domains granted to the requester; none while no grant is recorded (#112), which makes every domain one that does not exist for them.</summary>
    private static async Task<IReadOnlyCollection<int>> GrantedAsync(IGrantedDomains? grants, long requester, CancellationToken cancellationToken) =>
        grants is null ? new List<int>() : await grants.OfAsync(requester, cancellationToken);

    private static long? SessionOf(HttpContext context) => context.RequestServices.GetRequiredService<SessionStore>().Resolve(SessionCookie.Read(context.Request));

    /// <summary>The body is bounded by the most bytes a file may have, and a margin for the fields and the boundaries, before a byte of it is read.</summary>
    private static void Bound(HttpContext context, long maxBytes)
    {
        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } feature)
        {
            feature.MaxRequestBodySize = maxBytes + BodyMargin;
        }
    }
}
