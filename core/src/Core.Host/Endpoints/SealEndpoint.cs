using Core.Persistence.Custody;
using Core.Persistence.Custody.Verification;
using Core.Platform.Sessions;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Core.Host.Endpoints;

/// <summary>
/// The seal of the manual situation, for the person who keeps it outside the base: the route that issues the seal of the chain as it stands, writing nothing, and the route that records the
/// seal the person kept, as an act of the person of the session and only if the chain has that event with that digest. Routes of a live session, and of the manual situation only: in the
/// others the host sends the seal itself, and a seal recorded by hand would say a destination holds what it does not.
/// </summary>
internal static class SealEndpoint
{
    internal const string Route = "/custody/seal";
    internal const string ConfirmationRoute = Route + "/confirmation";

    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapGet(Route, IssueAsync);
        _ = endpoints.MapPost(ConfirmationRoute, ConfirmAsync);
    }

    /// <summary>With no database declared, or one that cannot be read, the answer is 503.</summary>
    private static async Task<IResult> IssueAsync(
        [FromServices] ChainVerificationService verification, [FromServices] SealDeclaration declared, [FromServices] ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        if (Refusal(verification, declared) is { } refusal)
        {
            return refusal;
        }

        try
        {
            var made = await verification.Sealer!.MakeAsync(cancellationToken);
            return Results.Ok(new SealIssued(made.ToText(), made.SealedAt, made.Events));
        }
        catch (Exception unreadable) when (unreadable is NpgsqlException or InvalidOperationException or TimeoutException)
        {
            return Unavailable(loggers, unreadable);
        }
    }

    /// <summary>A text that is not a seal is 400; a seal the chain does not have, or one the chain refuses to record, is 409 with the reason and nothing written.</summary>
    private static async Task<IResult> ConfirmAsync([FromBody] SealAsked asked, HttpRequest request, [AsParameters] CustodySources sources, CancellationToken cancellationToken)
    {
        var (verification, declared, sessions, _, loggers) = sources;
        if (Refusal(verification, declared) is { } refusal)
        {
            return refusal;
        }

        var reading = ChainSeal.Read(asked.Seal);
        if (reading.Seal is not { } seal)
        {
            return Results.BadRequest(new SealRefused("seal", reading.Problem.ToString()!, reading.Refusal!));
        }

        if (sessions.Resolve(SessionCookie.Read(request)) is not { } person)
        {
            return Results.Unauthorized();
        }

        try
        {
            var author = CustodyAuthor.Person(person);
            var recording = await verification.Sealer!.RecordAsync(seal, author, cancellationToken);
            if (recording.Seal is null)
            {
                return Results.Conflict(new SealRefused("seal", recording.Recorded.Outcome.ToString(), recording.Recorded.Refusal!));
            }

            await verification.RefreshSealWarningAsync(cancellationToken);
            return Results.Ok(new SealConfirmed(recording.Recorded.Sequence!.Value, seal.Events, seal.SealedAt));
        }
        catch (Exception unreadable) when (unreadable is NpgsqlException or InvalidOperationException or TimeoutException)
        {
            return Unavailable(loggers, unreadable);
        }
    }

    private static IResult? Refusal(ChainVerificationService verification, SealDeclaration declared)
    {
        if (declared.Situation != SealSituation.Manual)
        {
            return Results.Conflict(new SealRefused("situation", declared.Situation.ToString(), "o selo só é emitido e confirmado à mão quando a instalação declarou Manual"));
        }

        return verification.Sealer is null ? Results.StatusCode(StatusCodes.Status503ServiceUnavailable) : null;
    }

    private static IResult Unavailable(ILoggerFactory loggers, Exception failure)
    {
        loggers.CreateLogger(nameof(SealEndpoint)).SealRouteUnavailable(failure.GetType().Name);
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
}
