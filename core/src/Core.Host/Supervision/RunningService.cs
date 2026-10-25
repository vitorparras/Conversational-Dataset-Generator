using System.Diagnostics;

namespace Core.Host.Supervision;

/// <summary>The service process and the write end of its standard input, which stays open for as long as the core lives.</summary>
internal sealed class RunningService : IDisposable
{
    private static readonly TimeSpan StopGrace = TimeSpan.FromSeconds(5);

    private readonly Process process;

    internal RunningService(Process process, int port)
    {
        this.process = process;
        Port = port;
    }

    internal int Port { get; }

    internal int ProcessId => process.Id;

    internal bool HasExited => process.HasExited;

    internal int ExitCode => process.ExitCode;

    /// <summary>Completes when the process ends, whoever ends it.</summary>
    internal Task WaitForExitAsync() => process.WaitForExitAsync();

    /// <summary>Closes the standard input, which tells the service its parent is gone, and kills it if it does not leave.</summary>
    public void Dispose()
    {
        process.StandardInput.Close();
        if (!process.WaitForExit(StopGrace))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }

        process.Dispose();
    }
}
