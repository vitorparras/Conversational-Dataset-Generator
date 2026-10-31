using System.Diagnostics;

namespace Core.Host.Tests.Privacy;

/// <summary>The files of the working copy the host was built from: the ones git tracks and the ones it would add, never the ones it ignores.</summary>
internal static class RepositoryFiles
{
    /// <summary>The top folder of the working copy.</summary>
    public static string Root() => Git(AppContext.BaseDirectory, "rev-parse", "--show-toplevel").Trim();

    /// <summary>The full path of every file of the working copy that git tracks or does not ignore.</summary>
    public static IReadOnlyList<string> All()
    {
        var root = Root();
        var listed = Git(root, "ls-files", "-z", "--cached", "--others", "--exclude-standard");
        return [.. listed.Split('\0', StringSplitOptions.RemoveEmptyEntries).Select(path => Path.Combine(root, path))];
    }

    private static string Git(string folder, params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = folder, RedirectStandardOutput = true, UseShellExecute = false };
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
