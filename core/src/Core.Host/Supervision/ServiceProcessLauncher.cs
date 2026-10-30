using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Core.Host.Supervision;

/// <summary>
/// Starts the language service and hands it the secret through standard input, never through the command line. The
/// service opens its own port, the system choosing it, and tells the core which on its standard output, so the port
/// is never chosen and then released for the service to come and take.
/// </summary>
internal sealed class ServiceProcessLauncher
{
    private const string Module = "language_service";
    private const string SystemChoosesPort = "0";

    private readonly string executable;
    private readonly ErrorCollector? errors;

    /// <param name="executable">The interpreter that runs the service.</param>
    /// <param name="errors">Takes the standard error of the service; without one the service shares the standard error of the core, as it does in production.</param>
    internal ServiceProcessLauncher(string executable, ErrorCollector? errors = null)
    {
        this.executable = executable;
        this.errors = errors;
    }

    /// <summary>The arguments of the interpreter: isolated mode first, so that PYTHONHOME, PYTHONPATH and the user site the host inherited do not reach the service.</summary>
    internal static IReadOnlyList<string> Arguments() => ["-I", "-m", Module, "--port", SystemChoosesPort];

    /// <summary>How the process is started: the standard error is redirected only when a collector was given.</summary>
    internal ProcessStartInfo StartInfo()
    {
        var info = new ProcessStartInfo(executable)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = errors is not null,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardOutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardErrorEncoding = errors is null ? null : ErrorCollector.Encoding,
            UseShellExecute = false,
        };
        foreach (var argument in Arguments())
        {
            info.ArgumentList.Add(argument);
        }

        return info;
    }

    /// <summary>The running service, which tells its port a little after it starts, or null when the system could not start the executable.</summary>
    internal RunningService? Start(string secret)
    {
        var process = Spawn(StartInfo());
        if (process is null)
        {
            return null;
        }

        Deliver(process, secret);
        return RunningService.Announcing(process, errors is null ? null : new ErrorTail(process.StandardError, errors.Limit));
    }

    private static Process? Spawn(ProcessStartInfo info)
    {
        try
        {
            return Process.Start(info);
        }
        catch (Win32Exception)
        {
            return null;
        }
    }

    private static void Deliver(Process process, string secret)
    {
        try
        {
            process.StandardInput.WriteLine(secret);
        }
        catch (IOException)
        {
            // The process is already gone; the wait for its health reports how it ended.
        }
    }
}
