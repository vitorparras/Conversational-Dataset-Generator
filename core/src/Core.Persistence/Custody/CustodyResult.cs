namespace Core.Persistence.Custody;

/// <summary>The answer of an attempt to add an event: how it ended, the sequence of the event when it was added, and the reason in words when it was refused.</summary>
public sealed record CustodyResult
{
    /// <summary>How the attempt ended.</summary>
    public required CustodyOutcome Outcome { get; init; }

    /// <summary>The sequence the event took in the chain; null when it was refused.</summary>
    public long? Sequence { get; init; }

    /// <summary>The reason of the refusal, for whoever operates; null when the event was added.</summary>
    public string? Refusal { get; init; }

    internal static CustodyResult Appended(long sequence) => new() { Outcome = CustodyOutcome.Appended, Sequence = sequence };

    internal static CustodyResult Refused(CustodyOutcome outcome, string refusal) => new() { Outcome = outcome, Refusal = refusal };
}
