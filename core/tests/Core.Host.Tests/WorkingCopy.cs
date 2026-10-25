using System.Diagnostics;

namespace Core.Host.Tests;

/// <summary>What git says, at test time, about the working copy the host was built from.</summary>
internal static class WorkingCopy
{
    public static string Head() => Git("rev-parse", "HEAD").Trim();

    /// <summary>The tree state as the build names it: dirty when git lists any change not yet committed.</summary>
    public static string TreeState() => Git("--no-optional-locks", "status", "--porcelain").Length == 0 ? "Clean" : "Dirty";

    private static string Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = AppContext.BaseDirectory,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var git = Process.Start(info) ?? throw new InvalidOperationException("git did not start.");
        var output = git.StandardOutput.ReadToEnd();
        git.WaitForExit();
        return git.ExitCode == 0 ? output : throw new InvalidOperationException($"git {arguments[0]} exited with {git.ExitCode}.");
    }
}
