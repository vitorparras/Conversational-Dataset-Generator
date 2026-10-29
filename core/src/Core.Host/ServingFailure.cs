using Core.Platform.Startup;

namespace Core.Host;

/// <summary>Tells the operator the process ended on its own once the host was assembled, naming only the kind of failure: its message may carry text from a request.</summary>
internal static class ServingFailure
{
    /// <summary>Whether the exception is one the process can still report; an exhausted memory or a corrupted one is not, and keeps its own end.</summary>
    internal static bool CanReport(Exception exception) =>
        exception is not (OutOfMemoryException or AccessViolationException or InsufficientExecutionStackException);

    internal static ExitCode Report(TextWriter output, Exception exception)
    {
        output.WriteLine($"O processo parou: uma falha o encerrou ao abrir a porta ou ao servir ({exception.GetType().FullName}).");
        return ExitCode.ServingFailed;
    }
}
