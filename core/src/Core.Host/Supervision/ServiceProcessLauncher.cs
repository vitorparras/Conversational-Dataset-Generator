using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Core.Host.Supervision;

/// <summary>Starts the language service on a free loopback port and hands it the secret through standard input, never through the command line.</summary>
internal sealed class ServiceProcessLauncher
{
    private const string Module = "language_service";

    private readonly string executable;

    internal ServiceProcessLauncher(string executable)
    {
        this.executable = executable;
    }

    /// <summary>The running service, or null when the system could not start the executable.</summary>
    internal RunningService? Start(string secret)
    {
        var port = FreePort();
        var info = new ProcessStartInfo(executable)
        {
            RedirectStandardInput = true,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            UseShellExecute = false,
        };
        foreach (var argument in (string[])["-m", Module, "--port", port.ToString(CultureInfo.InvariantCulture)])
        {
            info.ArgumentList.Add(argument);
        }

        var process = Spawn(info);
        if (process is null)
        {
            return null;
        }

        Deliver(process, secret);
        return new RunningService(process, port);
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

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
