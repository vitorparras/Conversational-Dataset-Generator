using System.Diagnostics;

namespace Core.Platform.Tests.Logging;

/// <summary>A process started by a test: awaited with a limit, and killed with its whole tree the moment the limit passes.</summary>
internal sealed class ExternalProcess : IDisposable
{
    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(5);

    private readonly Process process;
    private readonly Task<string> output;
    private readonly Task<string> error;

    private ExternalProcess(Process process)
    {
        this.process = process;
        output = process.StandardOutput.ReadToEndAsync();
        error = process.StandardError.ReadToEndAsync();
    }

    public bool HasExited => process.HasExited;

    public static ExternalProcess Start(ProcessStartInfo startInfo)
    {
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.UseShellExecute = false;
        return new(Process.Start(startInfo) ?? throw new InvalidOperationException("The process did not start."));
    }

    /// <summary>Runs the command to its end and hands over the outcome; a command that outlives the limit is killed and the wait fails.</summary>
    public static async Task<ProcessOutcome> RunAsync(ProcessStartInfo startInfo, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var process = Start(startInfo);
        return await process.WaitForExitAsync(timeout, cancellationToken);
    }

    public async Task<ProcessOutcome> WaitForExitAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var exited = process.WaitForExitAsync(cancellationToken);
        if (await Task.WhenAny(exited, Task.Delay(timeout, cancellationToken)) != exited)
        {
            Kill();
            throw new TimeoutException($"The process did not exit within {timeout}; it was killed.");
        }

        await exited;
        return new ProcessOutcome(process.ExitCode, await output + await error);
    }

    public void Dispose()
    {
        Kill();
        _ = Task.WaitAll([output, error], DrainTimeout);
        process.Dispose();
    }

    private void Kill()
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }
    }
}
