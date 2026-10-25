namespace Core.Platform.Startup;

/// <summary>What one startup check concluded.</summary>
public enum CheckStatus
{
    /// <summary>The thing the check looks for is there.</summary>
    Passed = 0,

    /// <summary>The thing the check looks for is missing or unusable.</summary>
    Failed = 1,

    /// <summary>The installation does not declare the thing the check looks for, so nothing was checked; not a failure.</summary>
    NotConfigured = 2,
}
