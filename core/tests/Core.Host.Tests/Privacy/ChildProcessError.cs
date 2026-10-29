using System.Diagnostics;

namespace Core.Host.Tests.Privacy;

/// <summary>A child process that writes a text to its error stream, as a service that leaked it would.</summary>
internal static class ChildProcessError
{
    /// <summary>What the child wrote to its error stream; the text must be free of shell characters.</summary>
    public static string Written(string text)
    {
        var info = new ProcessStartInfo(OperatingSystem.IsWindows() ? "cmd" : "sh") { RedirectStandardError = true, UseShellExecute = false };
        info.ArgumentList.Add(OperatingSystem.IsWindows() ? "/c" : "-c");
        info.ArgumentList.Add(OperatingSystem.IsWindows() ? $"echo {text} 1>&2" : $"echo {text} >&2");
        using var child = Process.Start(info) ?? throw new InvalidOperationException("The child process did not start.");
        var error = child.StandardError.ReadToEnd();
        child.WaitForExit();
        return child.ExitCode == 0
            ? error
            : throw new InvalidOperationException(
                $"The child process ended with code 0x{child.ExitCode:X8} and wrote {error.Length} characters to its error stream: it did not run, so there is nothing to sweep.");
    }
}
