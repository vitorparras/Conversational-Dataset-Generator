namespace Core.Persistence.Ingestion;

/// <summary>The numbers of a batch for its detail, read while it is open as after: numbers and codes alone, never a word of the material.</summary>
public sealed record BatchDetailCounts
{
    /// <summary>How many items took each form, the declared one or the one an accommodated item received, by the code of the form.</summary>
    public required IReadOnlyList<BatchCount> Forms { get; init; }

    /// <summary>How many passages go to each destination, in the order remove, pseudonymize, preserve.</summary>
    public required IReadOnlyList<BatchCount> Destinations { get; init; }

    /// <summary>How many accommodated items declared each loss, by the form received and the codes of the loss, joined by a colon.</summary>
    public required IReadOnlyList<BatchCount> Accommodations { get; init; }

    /// <summary>How many messages were marked as a repetition of another.</summary>
    public required long Marked { get; init; }

    /// <summary>How many first messages the marked ones repeat.</summary>
    public required long Firsts { get; init; }

    /// <summary>The version of the list of steps that sanitized the items of the batch; none when no item was sanitized.</summary>
    public string? SanitizationVersion { get; init; }

    /// <summary>What each step of that version did to the items, in the order the steps ran.</summary>
    public IReadOnlyList<BatchStepCount> Steps { get; init; } = [];
}
