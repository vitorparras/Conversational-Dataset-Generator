namespace Core.Persistence.Ingestion;

/// <summary>The approval of a batch: the review of the whole sample found nothing, and the batch is accepted with it, in one step.</summary>
public sealed record BatchApproval(int DomainId, long BatchId, long ReviewerId);
