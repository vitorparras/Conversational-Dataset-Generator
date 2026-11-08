using Core.Platform.Startup;

namespace Core.Host;

/// <summary>Writes one line for each check of the startup, in the order they ran, when the host is assembled; a process with no session to read <c>/status</c> keeps them in its log.</summary>
internal static class StartupCheckLine
{
    internal static void Write(ILogger logger, StartupReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        foreach (var check in report.Results)
        {
            logger.CheckConcluded(check.Name, check.Status);
        }
    }
}
