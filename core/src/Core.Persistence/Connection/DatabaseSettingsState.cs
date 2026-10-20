namespace Core.Persistence.Connection;

/// <summary>Whether the installation declares a database, and whether what it declares can be used.</summary>
public enum DatabaseSettingsState
{
    /// <summary>No <c>Database:Host</c>: no server guarantee is checked, and the status says so.</summary>
    NotConfigured = 0,

    /// <summary>The host is declared without the rest, or with a port that is not a number.</summary>
    Refused = 1,

    /// <summary>Everything the connection needs is declared.</summary>
    Configured = 2,
}
