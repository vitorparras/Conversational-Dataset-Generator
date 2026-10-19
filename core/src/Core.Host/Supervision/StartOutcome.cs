namespace Core.Host.Supervision;

/// <summary>How the start of the service ended.</summary>
internal enum StartOutcome
{
    /// <summary>The service answered the health route with success.</summary>
    Started = 0,

    /// <summary>The system could not start the executable.</summary>
    NotStarted = 1,

    /// <summary>The process ended before it answered.</summary>
    ProcessExited = 2,

    /// <summary>The time limit passed with the process still running and not answering.</summary>
    TimedOut = 3,
}
