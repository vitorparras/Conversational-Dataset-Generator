namespace Core.Platform.Startup;

/// <summary>What one startup check concluded.</summary>
public enum CheckStatus
{
    /// <summary>The thing the check looks for is there.</summary>
    Passed = 0,

    /// <summary>The thing the check looks for is missing or unusable.</summary>
    Failed = 1,
}
