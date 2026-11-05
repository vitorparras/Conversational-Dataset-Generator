namespace Core.Persistence.Migrations;

/// <summary>Whether the run opened the window of the owner credential in the chain of custody, and when.</summary>
public enum OwnerWindowOpening
{
    /// <summary>No window: nothing was pending, or the database was empty.</summary>
    None,

    /// <summary>The event was written before the first step, with the chain verified and its tip kept.</summary>
    Opened,

    /// <summary>The database was older than the step that creates the events: the event was written right after that step, with the tip read before it.</summary>
    OpenedAfterTheStep,
}
