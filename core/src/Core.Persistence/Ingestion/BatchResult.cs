using Core.Persistence.Custody;

namespace Core.Persistence.Ingestion;

/// <summary>The answer of an attempt to open or to close a batch.</summary>
public sealed record BatchResult
{
    /// <summary>How the attempt ended.</summary>
    public required BatchOutcome Outcome { get; init; }

    /// <summary>The batch opened or closed; null when nothing was written.</summary>
    public long? BatchId { get; init; }

    /// <summary>The sequence the event took in the chain; null when nothing was written.</summary>
    public long? Sequence { get; init; }

    /// <summary>What the chain answered when it refused the event; null otherwise.</summary>
    public CustodyResult? Refusal { get; init; }
}
