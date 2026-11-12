namespace Core.Persistence.Ingestion;

/// <summary>
/// A batch as the register of the submissions shows it: its state, why it was rejected, who opened it and when, and what the submission brought.
/// Nothing in it is a word of the material or the name of the file.
/// </summary>
public sealed record BatchEntry
{
    /// <summary>The batch.</summary>
    public required long Id { get; init; }

    /// <summary>The domain the batch belongs to.</summary>
    public required int DomainId { get; init; }

    /// <summary>Where the batch is.</summary>
    public required BatchState State { get; init; }

    /// <summary>The motive of the rejection, a name; null while the batch is not rejected.</summary>
    public string? Reason { get; init; }

    /// <summary>The person who submitted.</summary>
    public required long SubmittedBy { get; init; }

    /// <summary>When the submission came, by the clock of the server.</summary>
    public required DateTimeOffset SubmittedAt { get; init; }

    /// <summary>What the submission brought; null for a batch no submission opened.</summary>
    public SubmissionReceipt? Receipt { get; init; }

    /// <summary>For a repeat, the first batch of the domain that accepted the same file: the one that recognized it by the digest and not by the name; null for any other batch.</summary>
    public long? PreviousBatch { get; init; }

    /// <summary>
    /// Whether the submission declared where the material came from, or chose to say it is unknown: <c>declared</c> or <c>unknown</c>, never the text; null for a batch no submission opened.
    /// </summary>
    public string? OriginState { get; init; }
}
