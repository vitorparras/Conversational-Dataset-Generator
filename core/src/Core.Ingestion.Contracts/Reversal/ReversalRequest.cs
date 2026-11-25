namespace Core.Ingestion.Contracts.Reversal;

/// <summary>A request to reverse a marker that the rule accepted: one marker, in one item, why, what for and for how long.</summary>
public sealed record ReversalRequest
{
    /// <summary>The item the marker is in.</summary>
    public required long ItemId { get; init; }

    /// <summary>The marker, as the pseudonymized text writes it.</summary>
    public required string Marker { get; init; }

    /// <summary>For how many days the identity is asked for.</summary>
    public required int TermDays { get; init; }

    /// <summary>Why the analysis does not advance without the identity.</summary>
    public required string Reason { get; init; }

    /// <summary>What will be done with the identity.</summary>
    public required string Use { get; init; }
}
