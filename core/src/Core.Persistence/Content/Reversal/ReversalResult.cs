using Core.Ingestion.Contracts.Reversal;
using Core.Persistence.Custody;

namespace Core.Persistence.Content.Reversal;

/// <summary>What became of an act on a request, and what the screen shows of it.</summary>
/// <param name="Outcome">What became of the act.</param>
public sealed record ReversalResult(ReversalOutcome Outcome)
{
    /// <summary>The row written: the request asked, the authorization given, or the request declined.</summary>
    public long? Id { get; init; }

    /// <summary>The fields an incomplete request lacks.</summary>
    public IReadOnlyList<ReversalRequestField> Missing { get; init; } = [];

    /// <summary>How wide a request too wide is.</summary>
    public ReversalScope? Scope { get; init; }

    /// <summary>Why the chain did not record the act.</summary>
    public CustodyResult? Refusal { get; init; }
}
