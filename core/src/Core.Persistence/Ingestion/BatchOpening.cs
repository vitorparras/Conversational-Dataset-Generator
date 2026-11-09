namespace Core.Persistence.Ingestion;

/// <summary>
/// Which batch opens, in which domain, by whom, under which condition of origin and, when a submission opens it, what the submission brought and where it says it came from.
/// A batch that a submission opens has its receipt and its origin together, and one that none opens has neither: the type refuses any other.
/// </summary>
public sealed record BatchOpening
{
    /// <summary>The opening of a batch; the receipt and the origin come together or not at all.</summary>
    /// <param name="domainId">The domain the batch belongs to.</param>
    /// <param name="authorId">The person who opens it, who must be active.</param>
    /// <param name="condition">The condition the material comes under, one the installation knows; null when the opening declares none, and the batch then takes the most restrictive one.</param>
    /// <param name="receipt">What the submission brought, kept with the batch; null for a batch that no submission opens.</param>
    /// <param name="origin">Where the submission says the material came from, sealed or declared unknown; null for a batch that no submission opens.</param>
    /// <param name="rejectedFor">The motive (a name of lowercase letters, digits and underscores) when the batch is turned back as it opens, in the same transaction; null when it stays open.</param>
    public BatchOpening(int domainId, long authorId, string? condition = null, SubmissionReceipt? receipt = null, OriginDeclaration? origin = null, string? rejectedFor = null)
    {
        if ((receipt is null) != (origin is null))
        {
            throw new ArgumentException("A batch that a submission opens has its receipt and its origin together, and one that none opens has neither.", nameof(receipt));
        }

        (DomainId, AuthorId, Condition, Receipt, Origin, RejectedFor) = (domainId, authorId, condition, receipt, origin, rejectedFor);
    }

    /// <summary>The domain the batch belongs to.</summary>
    public int DomainId { get; }

    /// <summary>The person who opens it.</summary>
    public long AuthorId { get; }

    /// <summary>The condition the material comes under; null when none is declared.</summary>
    public string? Condition { get; init; }

    /// <summary>What the submission brought; null for a batch that no submission opens.</summary>
    public SubmissionReceipt? Receipt { get; }

    /// <summary>Where the submission says the material came from; null for a batch that no submission opens.</summary>
    public OriginDeclaration? Origin { get; }

    /// <summary>The motive the batch is rejected with as it opens; null when it stays open.</summary>
    public string? RejectedFor { get; }
}
