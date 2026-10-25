namespace Core.Host.Supervision;

/// <summary>Where the language service stands, as the supervisor sees it.</summary>
internal enum ServiceState
{
    /// <summary>The first start is under way; the service has not answered yet.</summary>
    Starting = 0,

    /// <summary>The service answered its health route and its process is still running.</summary>
    Available = 1,

    /// <summary>The service did not answer, or its process ended after answering.</summary>
    Unavailable = 2,

    /// <summary>A restart was asked for; the previous process is being replaced by one with a new secret.</summary>
    Restarting = 3,

    /// <summary>The core is stopping and has ended the service.</summary>
    Stopped = 4,
}
