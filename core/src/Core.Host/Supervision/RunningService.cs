using System.Diagnostics;

namespace Core.Host.Supervision;

/// <summary>The service process and the write end of its standard input, which stays open for as long as the core lives.</summary>
internal sealed class RunningService : IDisposable
{
    private static readonly TimeSpan StopGrace = TimeSpan.FromSeconds(5);

    private readonly Process process;
    private readonly TaskCompletionSource<bool> announced = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int port;

    /// <summary>A process whose port is already known.</summary>
    internal RunningService(Process process, int port)
    {
        this.process = process;
        this.port = port;
        _ = announced.TrySetResult(true);
    }

    private RunningService(Process process)
    {
        this.process = process;
    }

    /// <summary>The end of the standard error of the service, when a collector was asked for it; null otherwise.</summary>
    internal ErrorTail? Errors { get; private set; }

    /// <summary>The port the service listens on, which the service itself told; 0 until it did.</summary>
    internal int Port => Volatile.Read(ref port);

    internal int ProcessId => process.Id;

    internal bool HasExited => process.HasExited;

    internal int ExitCode => process.ExitCode;

    /// <summary>
    /// A service that chose its own port and says which on its standard output: nobody else writes there, so the port
    /// comes from the process that holds it, never from a choice the core made and released.
    /// </summary>
    internal static RunningService Announcing(Process process, ErrorTail? errors = null)
    {
        var service = new RunningService(process) { Errors = errors };
        _ = service.ReadAnnouncementAsync();
        return service;
    }

    /// <summary>Completes with true once the port is known and with false when the standard output ended or began with anything but the announcement.</summary>
    internal Task<bool> PortAnnouncedAsync() => announced.Task;

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

    /// <summary>Reads the announcement, then drains whatever follows, so that the pipe never fills and holds the service.</summary>
    private async Task ReadAnnouncementAsync()
    {
        try
        {
            var first = PortAnnouncement.PortIn(await process.StandardOutput.ReadLineAsync());
            if (first is { } announcedPort)
            {
                Volatile.Write(ref port, announcedPort);
            }

            _ = announced.TrySetResult(first is not null);
            while (await process.StandardOutput.ReadLineAsync() is not null)
            {
                // Nothing after the announcement is for the core.
            }
        }
        catch (Exception ended) when (ended is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // The process is gone or released; the wait for its health reports how it ended.
        }
        finally
        {
            _ = announced.TrySetResult(false);
        }
    }
}
