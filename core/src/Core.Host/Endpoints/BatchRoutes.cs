namespace Core.Host.Endpoints;

/// <summary>The routes of the batches for the curation: the list, the entry of the material and the review of the sample, which the host maps together.</summary>
internal static class BatchRoutes
{
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        BatchScreensEndpoint.Map(endpoints);
        SampleReviewEndpoint.Map(endpoints);
    }
}
