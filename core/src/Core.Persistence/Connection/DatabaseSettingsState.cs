namespace Core.Persistence.Connection;

/// <summary>Whether what the installation declares for the database can be used.</summary>
public enum DatabaseSettingsState
{
    /// <summary>A key is missing, or the host is declared with a port that is not a number.</summary>
    Refused = 0,

    /// <summary>Everything the connection needs is declared.</summary>
    Configured = 1,
}
