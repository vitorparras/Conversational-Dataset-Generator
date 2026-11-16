using Core.Host.Review;
using Core.Persistence.Domains;
using Core.Persistence.Ingestion;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>What the routes of the review of the sample take from the host services: the desk, the batches and the domains granted; each absent with no database.</summary>
internal sealed record SampleReviewSources(
    [FromServices] SampleReviewDesk? Desk,
    [FromServices] BatchRegister? Register,
    [FromServices] DomainListing? Listing);
