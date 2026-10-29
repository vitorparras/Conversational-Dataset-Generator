using Core.Platform.Startup;

namespace Core.Host;

/// <summary>Tells the operator that the process ended on its own after it had begun to serve, naming only the kind of failure: its message may carry text from a request.</summary>
internal static class ServingFailure
{
    internal static ExitCode Report(TextWriter output, Exception exception)
    {
        output.WriteLine($"O processo parou: uma falha o encerrou depois de começar a servir ({exception.GetType().FullName}).");
        return ExitCode.ServingFailed;
    }
}
