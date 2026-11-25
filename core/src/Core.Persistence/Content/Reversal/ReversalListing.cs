namespace Core.Persistence.Content.Reversal;

/// <summary>Which requests of the domain a list shows: the person's own, or those of others that wait for a decision, narrowed and paged.</summary>
public sealed record ReversalListing
{
    /// <summary>The domain of the requests.</summary>
    public required int DomainId { get; init; }

    /// <summary>Who looks at the list.</summary>
    public required long PersonId { get; init; }

    /// <summary>Whether the list is of the person's own requests; otherwise, of those of others that are pending or expired.</summary>
    public required bool Mine { get; init; }

    /// <summary>Only the requests of this item, if given.</summary>
    public long? ItemId { get; init; }

    /// <summary>Only the requests in this state, if given.</summary>
    public ReversalState? State { get; init; }

    /// <summary>The page, counted from one.</summary>
    public int Page { get; init; } = 1;
}
