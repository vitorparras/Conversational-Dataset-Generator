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
}
