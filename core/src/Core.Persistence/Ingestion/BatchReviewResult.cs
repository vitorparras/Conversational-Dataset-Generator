using Core.Persistence.Custody;

namespace Core.Persistence.Ingestion;

/// <summary>The answer to a request about the review of a batch.</summary>
public sealed record BatchReviewResult
{
    /// <summary>What became of the request.</summary>
    public required BatchReviewOutcome Outcome { get; init; }

    /// <summary>The numbers of the items drawn, when the sample was drawn.</summary>
    public IReadOnlyList<long>? Sample { get; init; }

    /// <summary>The sequence of the event in the chain, when one was recorded.</summary>
    public long? Sequence { get; init; }

    /// <summary>Why the event was not recorded, when it was not.</summary>
    public CustodyResult? Refusal { get; init; }
}
