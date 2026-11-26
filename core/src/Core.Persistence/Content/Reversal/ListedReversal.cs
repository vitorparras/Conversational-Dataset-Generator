namespace Core.Persistence.Content.Reversal;

/// <summary>A request as a list or its page shows it: the scope, the term, who asked and when, and where it stands; never why, nor the identity.</summary>
public sealed record ListedReversal
{
    /// <summary>The request.</summary>
    public required long Id { get; init; }

    /// <summary>The item of the marker.</summary>
    public required long ItemId { get; init; }

    /// <summary>The marker to reverse.</summary>
    public required string Marker { get; init; }

    /// <summary>For how many days the reversal is asked.</summary>
    public required int TermDays { get; init; }

    /// <summary>Who asked.</summary>
    public required long RequestedBy { get; init; }

    /// <summary>When it was asked.</summary>
    public required DateTimeOffset RequestedAt { get; init; }

    /// <summary>Where it stands.</summary>
    public required ReversalState State { get; init; }

    /// <summary>Who gave the last authorization, if any.</summary>
    public long? AuthorizedBy { get; init; }

    /// <summary>Until when the last authorization holds, or held.</summary>
    public DateTimeOffset? ValidUntil { get; init; }

    /// <summary>The last authorization, if any.</summary>
    public long? AuthorizationId { get; init; }

    /// <summary>When the marker was revealed, if it was; never what it was.</summary>
    public DateTimeOffset? RevealedAt { get; init; }
}
