namespace Core.Persistence.Ingestion;

/// <summary>The approval of a batch: the review of the whole sample found nothing, and the batch is accepted with it, in one step.</summary>
/// <param name="DomainId">The domain of the batch.</param>
/// <param name="BatchId">The batch whose sample was reviewed.</param>
/// <param name="ReviewerId">The person who approves.</param>
public sealed record BatchApproval(int DomainId, long BatchId, long ReviewerId);
