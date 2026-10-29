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

    internal ServiceProcessLauncher(string executable)
    {
        this.executable = executable;
    }

    /// <summary>The arguments of the interpreter: isolated mode first, so that PYTHONHOME, PYTHONPATH and the user site the host inherited do not reach the service.</summary>
    internal static IReadOnlyList<string> Arguments() => ["-I", "-m", Module, "--port", SystemChoosesPort];

    /// <summary>The running service, which tells its port a little after it starts, or null when the system could not start the executable.</summary>
    internal RunningService? Start(string secret)
    {
        var info = new ProcessStartInfo(executable)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardOutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            UseShellExecute = false,
        };
        foreach (var argument in Arguments())
        {
            info.ArgumentList.Add(argument);
        }

        var process = Spawn(info);
        if (process is null)
        {
            return null;
        }

        Deliver(process, secret);
        return RunningService.Announcing(process);
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
