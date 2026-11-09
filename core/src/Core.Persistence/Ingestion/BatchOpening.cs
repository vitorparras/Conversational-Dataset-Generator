namespace Core.Persistence.Ingestion;

/// <summary>
/// Which batch opens, in which domain, by whom, under which condition of origin and, when a submission opens it, what the submission brought and where it says it came from.
/// A batch that a submission opens has its receipt and its origin together, and one that none opens has neither: the type has no other way to be built.
/// </summary>
public sealed record BatchOpening
{
    /// <summary>The opening of a batch that no submission opens, under no condition declared.</summary>
    /// <param name="domainId">The domain the batch belongs to.</param>
    /// <param name="authorId">The person who opens it, who must be active.</param>
    public BatchOpening(int domainId, long authorId)
        : this(domainId, authorId, null, null)
    {
    }

    /// <summary>The opening of a batch that no submission opens, under the condition given.</summary>
    /// <param name="domainId">The domain the batch belongs to.</param>
    /// <param name="authorId">The person who opens it, who must be active.</param>
    /// <param name="condition">The condition the material comes under, one the installation knows; null when none is declared, and the batch then takes the most restrictive one.</param>
    public BatchOpening(int domainId, long authorId, string? condition)
        : this(domainId, authorId, condition, null)
    {
    }

    /// <summary>The opening of a batch, with what a submission brought when one opens it.</summary>
    /// <param name="domainId">The domain the batch belongs to.</param>
    /// <param name="authorId">The person who opens it, who must be active.</param>
    /// <param name="condition">The condition the material comes under; null when none is declared.</param>
    /// <param name="submission">What the submission brought, receipt and origin together; null for a batch that no submission opens.</param>
    public BatchOpening(int domainId, long authorId, string? condition, SubmissionOpening? submission)
    {
        (DomainId, AuthorId, Condition, Submission) = (domainId, authorId, condition, submission);
    }

    /// <summary>The domain the batch belongs to.</summary>
    public int DomainId { get; }

    /// <summary>The person who opens it.</summary>
    public long AuthorId { get; }

    /// <summary>The condition the material comes under; null when none is declared.</summary>
    public string? Condition { get; init; }

    /// <summary>What the submission brought, receipt and origin together; null for a batch that no submission opens.</summary>
    public SubmissionOpening? Submission { get; }

    /// <summary>What the submission brought, kept with the batch; null for a batch that no submission opens.</summary>
    public SubmissionReceipt? Receipt => Submission?.Receipt;

    /// <summary>Where the submission says the material came from; null for a batch that no submission opens.</summary>
    public OriginDeclaration? Origin => Submission?.Origin;
}
