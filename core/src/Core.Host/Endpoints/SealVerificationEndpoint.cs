using Core.Persistence.Custody.Verification;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>
/// Verifies the chain of custody against the seal whoever operates brings, in its form in text, and says what that verification protects in the situation the installation declared.
/// A text that is not a seal is refused with 400 and the reason before anything runs. A route of a live session, like the reading of the last verification.
/// </summary>
internal static class SealVerificationEndpoint
{
    internal static void Map(IEndpointRouteBuilder endpoints) => _ = endpoints.MapPost(ChainVerificationEndpoint.Route, VerifyAsync);

    /// <summary>With no database declared, or one that cannot be read, the answer is 503: the chain was not compared with anything.</summary>
    private static async Task<IResult> VerifyAsync(
        [FromBody] SealAsked asked, [FromServices] ChainVerificationService verification, [FromServices] SealDeclaration declared, [FromServices] TimeProvider time,
        CancellationToken cancellationToken)
    {
        var reading = ChainSeal.Read(asked.Seal);
        if (reading.Seal is not { } seal)
        {
            return Results.BadRequest(new SealRefused("seal", reading.Problem.ToString()!, reading.Refusal!));
        }

        var done = await verification.VerifyAgainstAsync(seal, cancellationToken);
        if (done?.Seal is not { } standing)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var last = declared.Situation == SealSituation.Manual && verification.SealReader is { } reader ? await reader.LastAsync(cancellationToken) : null;
        return Results.Ok(SealVerificationShown.Of(done, standing, declared.Situation, SealAge.Warning(declared.Situation, last, time.GetUtcNow())));
    }
}
