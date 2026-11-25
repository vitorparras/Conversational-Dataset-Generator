namespace Core.Host.Endpoints;

/// <summary>The routes of the batches for the curation: the list, the entry of the material, the review of the sample, the rerun of the accepted ones and the reversal of a marker, mapped together.</summary>
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
