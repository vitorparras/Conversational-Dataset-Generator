namespace Core.Persistence.Custody;

/// <summary>How an attempt to add an event ended; every value but <see cref="Appended" /> is a refusal that wrote nothing.</summary>
public enum CustodyOutcome
{
    /// <summary>The event is in the chain.</summary>
    Appended,

    /// <summary>The person named as author is not registered.</summary>
    ActorUnknown,

    /// <summary>The person named as author was deactivated.</summary>
    ActorDeactivated,

    /// <summary>The event to correct is not in the chain.</summary>
    CorrectedNotFound,

    /// <summary>Another writer reached the same link first; nothing was written, and whoever asked may ask again.</summary>
    Conflict,

    /// <summary>The server refused to change, remove or empty away an event of the chain; nothing was written, and the refusal carries no content of any event.</summary>
    Immutable,
}
