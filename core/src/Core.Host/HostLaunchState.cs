namespace Core.Host;

/// <summary>Whether the host was assembled or refused before assembly.</summary>
internal enum HostLaunchState
{
    /// <summary>The configuration loaded and the host is ready to serve.</summary>
    Ready = 0,

    /// <summary>The configuration was refused; nothing was assembled.</summary>
    Refused = 1,
}
