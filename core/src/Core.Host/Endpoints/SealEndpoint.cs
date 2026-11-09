using Microsoft.AspNetCore.Mvc;

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
    private static async Task<IResult> IssueAsync([AsParameters] ChainSources sources, CancellationToken cancellationToken)
    {
        if (Refusal(sources) is { } refusal)
        {
            return refusal;
        }

        return await SealState.IssueAsync(sources, cancellationToken) is { } made
            ? Results.Ok(new SealIssued(made.ToText(), made.SealedAt, made.Events))
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    /// <summary>A text that is not a seal is 400; a seal the chain does not have, or one the chain refuses to record, is 409 with the reason and nothing written.</summary>
    private static async Task<IResult> ConfirmAsync([FromBody] SealAsked asked, HttpRequest request, [AsParameters] ChainSources sources, CancellationToken cancellationToken)
    {
        if (Refusal(sources) is { } refusal)
        {
            return refusal;
        }

        var confirmation = await SealState.ConfirmAsync(sources, SessionCookie.Read(request), asked.Seal, cancellationToken);
        return confirmation.Outcome switch
        {
            SealConfirmationOutcome.Recorded => Results.Ok(confirmation.Recorded),
            SealConfirmationOutcome.NotASeal => Results.BadRequest(new SealRefused("seal", confirmation.Kind, confirmation.Reason)),
            SealConfirmationOutcome.NotRecorded => Results.Conflict(new SealRefused("seal", confirmation.Kind, confirmation.Reason)),
            SealConfirmationOutcome.NoSession => Results.Unauthorized(),
            SealConfirmationOutcome.Unreadable => Results.StatusCode(StatusCodes.Status503ServiceUnavailable),
            _ => throw new ArgumentOutOfRangeException(nameof(asked), confirmation.Outcome, "The record ended in a way the route does not answer."),
        };
    }

    private static IResult? Refusal(ChainSources sources) => SealState.Availability(sources) switch
    {
        SealAvailability.NotManual => Results.Conflict(
            new SealRefused("situation", sources.Declared.Situation.ToString(), "o selo só é emitido e confirmado à mão quando a instalação declarou Manual")),
        SealAvailability.Unreadable => Results.StatusCode(StatusCodes.Status503ServiceUnavailable),
        SealAvailability.Open => null,
        _ => throw new ArgumentOutOfRangeException(nameof(sources), sources.Declared, "The seal is available in a way the route does not answer."),
    };
}
