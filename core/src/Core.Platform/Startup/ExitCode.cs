namespace Core.Platform.Startup;

/// <summary>What the process reports to whoever started it when it ends.</summary>
public enum ExitCode
{
    /// <summary>The process ran and ended as asked.</summary>
    Success = 0,

    /// <summary>A startup check failed, so the process did not open its port; 69 is the unavailable service of the sysexits convention.</summary>
    StartupCheckFailed = 69,

    /// <summary>The configuration was refused before anything else started; 78 is the configuration error of the sysexits convention.</summary>
    ConfigurationRefused = 78,

    /// <summary>The migration found the schema already at the last step and ran nothing.</summary>
    MigrationNothingToDo = 10,

    /// <summary>The migration refused: the registry holds a step this build does not carry.</summary>
    MigrationRefusedAhead = 11,

    /// <summary>The migration refused: a step already applied no longer has the content that was registered.</summary>
    MigrationRefusedDivergent = 12,

    /// <summary>A step of the migration failed and was undone; the steps before it stay applied.</summary>
    MigrationStepFailed = 13,

    /// <summary>The migration could not reach the server or read the registry, and ran nothing.</summary>
    MigrationNotConnected = 14,
}
