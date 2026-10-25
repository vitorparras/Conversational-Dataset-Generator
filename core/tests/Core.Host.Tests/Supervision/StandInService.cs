using System.Diagnostics;
using Core.Host.Supervision;

namespace Core.Host.Tests.Supervision;

/// <summary>A process that runs until its standard input closes, held the way the supervision holds the real service; it answers nothing.</summary>
internal static class StandInService
{
    public static RunningService Start()
    {
        var info = new ProcessStartInfo(LanguageServiceInterpreter.Path) { RedirectStandardInput = true, UseShellExecute = false };
        foreach (var argument in (string[])["-c", "import sys; sys.stdin.read()"])
        {
            info.ArgumentList.Add(argument);
        }

        return new RunningService(Process.Start(info) ?? throw new InvalidOperationException("The stand-in did not start."), port: 0);
    }

    /// <summary>Ends the process by its number, from outside, as another account or a crash would.</summary>
    public static void Kill(int id)
    {
        using var process = Process.GetProcessById(id);
        process.Kill();
    }
}
