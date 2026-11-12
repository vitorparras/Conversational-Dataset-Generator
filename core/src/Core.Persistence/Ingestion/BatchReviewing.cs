namespace Core.Persistence.Ingestion;

/// <summary>The review of the sample of a batch: it found nothing, or it found personal data not treated and the whole batch is rejected with it.</summary>
/// <param name="DomainId">The domain of the batch.</param>
/// <param name="BatchId">The batch whose sample was drawn.</param>
/// <param name="ReviewerId">The person who reviewed the sample.</param>
/// <param name="Finding">What was found, a name of lowercase letters, digits and underscores and never free text; null when the review found nothing.</param>
public sealed record BatchReviewing(int DomainId, long BatchId, long ReviewerId, string? Finding = null);
