using Core.Persistence.Custody;

namespace Core.Persistence.Content.Emptying;

/// <summary>The answer of a request to empty an item: how it ended, the sequence of the event it left in the chain, and what the chain said when it did not accept the event.</summary>
public sealed record EmptyingResult
{
    /// <summary>How the request ended.</summary>
    public required EmptyingOutcome Outcome { get; init; }

    /// <summary>The sequence of the event in the chain: the emptying, or the refusal of it; null when nothing was recorded.</summary>
    public long? Sequence { get; init; }

    /// <summary>The refusal of the chain, only when <see cref="EmptyingOutcome.NotRecorded" />.</summary>
    public CustodyResult? Refusal { get; init; }
}
