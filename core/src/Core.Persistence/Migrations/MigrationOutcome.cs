namespace Core.Persistence.Migrations;

/// <summary>How a run of the migration ended.</summary>
public enum MigrationOutcome
{
    /// <summary>Every pending step was applied and registered.</summary>
    Applied = 0,

    /// <summary>The registry already holds every step, unchanged; nothing was run.</summary>
    NothingToDo = 1,

    /// <summary>The registry holds a step this build does not know; nothing was run.</summary>
    RefusedAhead = 2,

    /// <summary>A step already applied no longer has the content that was registered; nothing was run.</summary>
    RefusedDivergent = 3,

    /// <summary>A step failed and was undone; the steps before it stay applied.</summary>
    StepFailed = 4,

    /// <summary>The connection or the reading of the registry failed before any step ran.</summary>
    NotConnected = 5,
}
