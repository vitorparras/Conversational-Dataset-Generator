namespace Core.Persistence.Ingestion;

/// <summary>The mark of a place of the sample as reviewed: the place right after the last one marked, counted from 1 in the order of the sample.</summary>
/// <param name="DomainId">The domain of the batch.</param>
/// <param name="BatchId">The batch whose sample is being reviewed.</param>
/// <param name="Place">The place of the sample to mark, from 1.</param>
public sealed record BatchMarking(int DomainId, long BatchId, int Place);
