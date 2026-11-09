namespace Core.Persistence.Ingestion;

/// <summary>Which batches to list: those of the domains given, by state and by the moment of the submission, in a page the server cuts.</summary>
public sealed record BatchFilter
{
    /// <summary>The domains whose batches may be seen; a batch of another is never listed.</summary>
    public required IReadOnlyCollection<int> Domains { get; init; }

    /// <summary>Only the batches in this state; null for any.</summary>
    public BatchState? State { get; init; }

    /// <summary>Only the batches submitted at or after this moment; null for any.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Only the batches submitted before this moment; null for any.</summary>
    public DateTimeOffset? Until { get; init; }

    /// <summary>How many batches of the newest to leave out.</summary>
    public int Skip { get; init; }

    /// <summary>How many batches the page holds.</summary>
    public int Take { get; init; } = 50;
}
