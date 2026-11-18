using Core.Host.Analysis;
using Core.Persistence.People;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>
/// Analyses one text: the concluded analysis, or the refusal with its cause and the reference a retry names to resume it. Only the technical administration reaches it (policy 006),
/// since the text is content which the audit does not read; it has no screen of its own.
/// </summary>
internal static class AnalysisEndpoint
{
    internal const string Route = "/analysis";

    private static readonly ScreenAccess Access = ScreenAccess.Unlisted(PersonFunctions.Administration);

    internal static void Map(IEndpointRouteBuilder endpoints) => _ = endpoints.MapPost(Route, AnalyzeAsync).WithMetadata(Access);

    /// <summary>A refusal answers 503, since what failed is the service the analysis depends on.</summary>
    private static async Task<IResult> AnalyzeAsync(
        [FromBody] AnalysisAsked asked, [FromServices] MaterialAnalysis analysis, CancellationToken cancellationToken)
    {
        var result = await analysis.AnalyzeAsync(asked, cancellationToken);
        return result.Concluded is { } concluded
            ? Results.Ok(concluded)
            : Results.Json(result.Refused, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}
