namespace Core.Platform.Keys;

/// <summary>A path with every link in it followed, those of the directories included, so that a link cannot hide where a file lies.</summary>
internal static class RealPath
{
    private const int MostLinks = 40;

    /// <summary>The full path to the file or folder itself; more than a few dozen links in one path is a loop and fails as one.</summary>
    internal static string Of(string path)
    {
        var full = Path.GetFullPath(path);
        var current = Path.GetPathRoot(full)!;
        var pending = new Stack<string>();
        Push(pending, full, current);
        for (var followed = 0; pending.Count > 0;)
        {
            var next = Path.Combine(current, pending.Pop());
            if (LinkTarget(next) is not { } target)
            {
                current = next;
                continue;
            }

            followed++;
            if (followed > MostLinks)
            {
                throw new IOException($"More than {MostLinks} links in one path.");
            }

            current = Path.GetPathRoot(target.FullName)!;
            Push(pending, target.FullName, current);
        }

        return current;
    }

    /// <summary>Where the link at the path points, one step; null when it is no link or when nothing is there, as for a folder not made yet.</summary>
    private static FileSystemInfo? LinkTarget(string path)
    {
        try
        {
            return File.ResolveLinkTarget(path, returnFinalTarget: false);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    /// <summary>The parts of the path after its root go on top of the stack, the first one on top.</summary>
    private static void Push(Stack<string> pending, string full, string root)
    {
        foreach (var part in Enumerable.Reverse(full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)))
        {
            pending.Push(part);
        }
    }
}
