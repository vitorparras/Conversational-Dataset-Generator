namespace Core.Persistence.Content.Reversal;

/// <summary>Where a request stands, as its rows and its events say at a moment.</summary>
public enum ReversalState
{
    /// <summary>Nobody decided it yet.</summary>
    Pending,

    /// <summary>Someone of the curation declined it.</summary>
    Declined,

    /// <summary>Its last authorization is in force.</summary>
    Authorized,

    /// <summary>Its last authorization expired; it may be authorized again.</summary>
    Expired,

    /// <summary>Its authorization served to reveal the marker; nothing more is done with it.</summary>
    Revealed,
}
