using System.Diagnostics;

namespace Core.Platform.Tests.Logging;

/// <summary>A command that stays alive far longer than any test waits for it.</summary>
internal static class Sleeper
{
    private const string Seconds = "300";

    public static ProcessStartInfo StartInfo() =>
        OperatingSystem.IsWindows()
            ? new ProcessStartInfo("cmd.exe") { ArgumentList = { "/c", "ping", "-n", Seconds, "127.0.0.1" } }
            : new ProcessStartInfo("sleep") { ArgumentList = { Seconds } };
}
