using System.ComponentModel;
using System.Diagnostics;

namespace Core.Host.Settings;

/// <summary>
/// Restarts the core by the command the installation configured (decision 0053): an absolute path and its arguments, run with no shell and with nothing from the database in them.
/// It tells only whether the command ended with success within the time; a command that does not start, ends with failure or does not end in time is a restart that did not happen.
/// </summary>
internal sealed class ServiceRestart
{
    internal static readonly TimeSpan Limit = TimeSpan.FromMinutes(2);

    private readonly string command;
    private readonly IReadOnlyList<string> arguments;

    /// <summary>The command to run and its arguments.</summary>
    internal ServiceRestart(string command, IReadOnlyList<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(arguments);
        this.command = command;
        this.arguments = arguments;
    }

    /// <summary>Whether the command ended with success.</summary>
    /// <param name="cancellationToken">Cancels the wait, and ends the command.</param>
    internal async Task<bool> RunAsync(CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(command) { UseShellExecute = false };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(start);
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            limit.CancelAfter(Limit);
            return process is not null && await Ended(process, limit.Token).ConfigureAwait(false);
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    private static async Task<bool> Ended(Process process, CancellationToken limit)
    {
        try
        {
            await process.WaitForExitAsync(limit).ConfigureAwait(false);
            return process.ExitCode == 0;
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            return false;
        }
    }
}
