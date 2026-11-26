using Core.Persistence.Custody;

namespace Core.Persistence.Content.Reversal;

/// <summary>
/// What became of a revelation: how it ended, when it was recorded, and the identity, only when the event is in the chain and the map was opened. A class and not a record, so
/// that nothing prints the identity by accident.
/// </summary>
public sealed class ReversalRevelation
{
    /// <summary>A revelation that ended as given.</summary>
    /// <param name="outcome">How it ended.</param>
    public ReversalRevelation(ReversalOutcome outcome) => Outcome = outcome;

    /// <summary>How it ended: recorded, denied with the reason, or not recorded at all.</summary>
    public ReversalOutcome Outcome { get; }

    /// <summary>When the revelation was recorded, only when it was.</summary>
    public DateTimeOffset? RevealedAt { get; init; }

    /// <summary>The original the marker stands for, to be handed over once and kept nowhere; only when the revelation was recorded and the map was opened.</summary>
    public string? Identity { get; init; }

    /// <summary>Why the chain did not record the revelation, or the reading of the map after it.</summary>
    public CustodyResult? Refusal { get; init; }
}
