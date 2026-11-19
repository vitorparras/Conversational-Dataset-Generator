using Core.Host.Review;
using Core.Persistence.Domains;
using Core.Persistence.Ingestion;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>What the routes of the review of the sample take from the host services: the desk, the batches, the domains granted and the counts of a batch; each absent with no database.</summary>
internal sealed record SampleReviewSources(
    [FromServices] SampleReviewDesk? Desk,
    [FromServices] BatchRegister? Register,
    [FromServices] DomainListing? Listing,
    [FromServices] BatchCounts? Counts);
