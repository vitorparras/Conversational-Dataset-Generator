namespace Core.Host;

/// <summary>Whether the host was assembled or stopped before assembly, and why.</summary>
internal enum HostLaunchState
{
    /// <summary>The configuration loaded, every check passed and the host is ready to serve.</summary>
    Ready = 0,

    /// <summary>The configuration was refused; nothing was assembled.</summary>
    Refused = 1,

    /// <summary>A startup check failed; nothing was assembled.</summary>
    CheckFailed = 2,
}
