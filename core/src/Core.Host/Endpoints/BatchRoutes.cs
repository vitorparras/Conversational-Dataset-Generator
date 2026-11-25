namespace Core.Host.Endpoints;

/// <summary>The routes of the batches for the curation, mapped together: the list, the entry of the material, the review, the rerun and the reversal of a marker.</summary>
internal static class BatchRoutes
{
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        BatchScreensEndpoint.Map(endpoints);
        SampleReviewEndpoint.Map(endpoints);
        RerunScreensEndpoint.Map(endpoints);
        ReversalScreensEndpoint.Map(endpoints);
    }
}
