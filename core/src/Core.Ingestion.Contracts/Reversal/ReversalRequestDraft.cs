namespace Core.Ingestion.Contracts.Reversal;

/// <summary>A request to reverse a marker as the form sent it, each field as written, before it is judged.</summary>
public sealed record ReversalRequestDraft
{
    /// <summary>The item, by its number; more than one, separated by a comma or a space, reaches more than one item.</summary>
    public string? Item { get; init; }

    /// <summary>The marker, as the pseudonymized text writes it; more than one, separated by a comma or a space, reaches more than one marker.</summary>
    public string? Marker { get; init; }

    /// <summary>For how many days the identity is asked for.</summary>
    public string? Term { get; init; }

    /// <summary>Why the analysis does not advance without the identity.</summary>
    public string? Reason { get; init; }

    /// <summary>What will be done with the identity.</summary>
    public string? Use { get; init; }
}
