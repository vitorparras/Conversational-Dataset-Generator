namespace Core.Persistence.Ingestion;

/// <summary>Which batch opens, in which domain, by whom, and under which condition of origin.</summary>
/// <param name="DomainId">The domain the batch belongs to.</param>
/// <param name="AuthorId">The person who opens it, who must be active.</param>
/// <param name="Condition">The condition the material comes under, one the installation knows; null when the opening declares none, and the batch then takes the most restrictive one.</param>
public sealed record BatchOpening(int DomainId, long AuthorId, string? Condition = null);
