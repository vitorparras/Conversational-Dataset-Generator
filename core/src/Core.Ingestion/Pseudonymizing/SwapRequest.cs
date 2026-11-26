namespace Core.Ingestion.Pseudonymizing;

/// <summary>What the swap of one item receives: the sanitized text, its occurrences with their referents and, when the item is pseudonymized again, the item and its map.</summary>
public sealed record SwapRequest
{
    /// <summary>The sanitized text, whose positions count code points.</summary>
    public required string Sanitized { get; init; }

    /// <summary>The occurrences of the sanitized text, each with its destination and its referent.</summary>
    public required IReadOnlyList<ReferredOccurrence> Occurrences { get; init; }

    /// <summary>The author of the message, whose name gets the marker of the author in the map even where the text does not mention it; null when the form has no author.</summary>
    public string? Author { get; init; }

    /// <summary>The item pseudonymized again; null when the item does not exist yet.</summary>
    public long? ItemId { get; init; }

    /// <summary>The map of the item pseudonymized again; null when the item has none yet.</summary>
    public ItemMap? Map { get; init; }

    /// <summary>The item and how many occurrences; never the text.</summary>
    public override string ToString() => $"SwapRequest {{ ItemId = {ItemId}, Occurrences = {Occurrences.Count} }}";
}
