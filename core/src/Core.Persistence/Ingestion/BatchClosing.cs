using Core.Persistence.Custody;

namespace Core.Persistence.Ingestion;

/// <summary>Which batch closes, in which domain, and who closes it.</summary>
/// <param name="DomainId">The domain of the batch.</param>
/// <param name="BatchId">The batch to close.</param>
/// <param name="Author">A person, who must be active, or the system.</param>
public sealed record BatchClosing(int DomainId, long BatchId, CustodyAuthor Author);
