namespace Core.Persistence.Content;

/// <summary>The batch the occurrences and the marks are written for, with the domain it belongs to.</summary>
/// <param name="DomainId">The domain of the batch.</param>
/// <param name="BatchId">The batch, which must be open.</param>
public sealed record ReplicationTarget(int DomainId, long BatchId);
