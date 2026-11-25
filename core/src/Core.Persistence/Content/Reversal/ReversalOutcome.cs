namespace Core.Persistence.Content.Reversal;

/// <summary>What became of an act on a request to reverse a marker.</summary>
public enum ReversalOutcome
{
    /// <summary>The act was written with its event.</summary>
    Recorded,

    /// <summary>The request lacks a field; nothing was written, and no event.</summary>
    Incomplete,

    /// <summary>The request reaches the domain, many items or many markers; the refusal is an event of its own.</summary>
    TooWide,

    /// <summary>The marker is not among those of the item; the refusal is an event of its own.</summary>
    MarkerNotInItem,

    /// <summary>Who acts does not hold the function of the curation; the refusal is an event of its own.</summary>
    NotCuration,

    /// <summary>Who asked for the reversal means to authorize it; the refusal is an event of its own.</summary>
    OwnRequest,

    /// <summary>The term of the authorization is not to come; nothing was written.</summary>
    TermInThePast,

    /// <summary>The domain holds no such request.</summary>
    NotFound,

    /// <summary>The request was declined, or holds an authorization in force; nothing was written.</summary>
    Closed,

    /// <summary>The chain did not record the act, and so nothing was written: the refusal says why, a chain broken among them.</summary>
    NotRecorded,
}
