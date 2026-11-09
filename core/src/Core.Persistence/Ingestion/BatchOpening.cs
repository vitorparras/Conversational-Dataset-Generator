namespace Core.Persistence.Ingestion;

/// <summary>Which batch opens, in which domain, by whom, under which condition of origin and, when a submission opens it, what the submission brought.</summary>
/// <param name="DomainId">The domain the batch belongs to.</param>
/// <param name="AuthorId">The person who opens it, who must be active.</param>
/// <param name="Condition">The condition the material comes under, one the installation knows; null when the opening declares none, and the batch then takes the most restrictive one.</param>
/// <param name="Receipt">What the submission brought, kept with the batch; null for a batch that no submission opens.</param>
public sealed record BatchOpening(int DomainId, long AuthorId, string? Condition = null, SubmissionReceipt? Receipt = null);
