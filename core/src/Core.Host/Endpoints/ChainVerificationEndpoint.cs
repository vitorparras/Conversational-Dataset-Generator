namespace Core.Host.Endpoints;

/// <summary>
/// Shows the last verification of the chain of custody as the chain records it, with the interval in force and whether one is late, and the history of the verifications with who asked for
/// each. A route of a live session and not part of the status, which answers with no session: the state of the chain and the count of its events are not for whoever did not enter.
/// What it answers is what <see cref="ChainState" /> reads, the same the screen of the chain shows.
/// </summary>
internal static class ChainVerificationEndpoint
{
    internal const string Route = "/custody/verification";

    internal static void Map(IEndpointRouteBuilder endpoints) => _ = endpoints.MapGet(Route, ShowAsync);

    /// <summary>With no database declared there is no chain to read, and one that cannot be read answers 503, with the type of the failure in the host's log.</summary>
    private static async Task<IResult> ShowAsync([AsParameters] ChainSources sources, CancellationToken cancellationToken) =>
        await ChainState.ReadAsync(sources, cancellationToken) is { } shown ? Results.Ok(shown) : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
}
