using Core.Persistence.Custody;

namespace Core.Persistence.Content.Rerun;

/// <summary>The answer of a request of a rerun: how it ended, the request written or the one open, and what the chain said when it did not record it.</summary>
/// <param name="Outcome">How the request ended.</param>
/// <param name="RerunId">The request written or the one open; none otherwise.</param>
public sealed record RerunRequestResult(RerunRequestOutcome Outcome, long? RerunId = null)
{
    /// <summary>What the chain said, only when <see cref="RerunRequestOutcome.NotRecorded"/>.</summary>
    public CustodyResult? Refusal { get; init; }
}
