namespace Core.Persistence.Ingestion;

/// <summary>
/// The request to draw the sample of a batch for review: the person who reviews does not choose the items, nor how many, only asks.
/// The size is the one the installation declares, and the service reads it once.
/// </summary>
/// <param name="DomainId">The domain of the batch.</param>
/// <param name="BatchId">The batch that waits for approval.</param>
/// <param name="ReviewerId">The person who asks for the review.</param>
public sealed record BatchSampling(int DomainId, long BatchId, long ReviewerId);
