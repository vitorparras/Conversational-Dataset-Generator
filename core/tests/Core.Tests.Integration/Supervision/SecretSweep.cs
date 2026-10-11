using Core.Tests.Shared;

namespace Core.Tests.Integration.Supervision;

/// <summary>Searches the places a secret could have been left for the secret itself.</summary>
internal static class SecretSweep
{
    private const long LargestFile = 8 * 1024 * 1024;

    /// <summary>
    /// The files that hold <paramref name="secret"/>: the installation (the folder the tests run
    /// from), the language service project and the system temporary folder. In the temporary
    /// folder only files written since <paramref name="since"/> are read, because that folder is
    /// shared with everything else on the machine.
    /// </summary>
    public static IReadOnlyList<string> FilesHolding(string secret, DateTime since)
    {
        var roots = new List<(string Label, string Path, bool OnlyRecent)>
        {
            ("installation", AppContext.BaseDirectory, false),
            ("language-service", Path.Combine(RepositoryRoot.Find().FullName, "language-service"), false),
            ("temp", Path.GetTempPath(), true),
        };

        var found = new List<string>();
        foreach (var (label, root, onlyRecent) in roots)
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System,
            };
            foreach (var path in Directory.EnumerateFiles(root, "*", options))
            {
                if (path.Contains($"{Path.DirectorySeparatorChar}.venv{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }

                if (Holds(path, secret, onlyRecent ? since : null))
                {
                    found.Add($"{label}:{Path.GetRelativePath(root, path)}");
                }
            }
        }

        return found;
    }

    private static bool Holds(string path, string secret, DateTime? since)
    {
        try
        {
            var info = new FileInfo(path);
            if (info.Length > LargestFile || (since is { } limit && info.LastWriteTimeUtc < limit))
            {
                return false;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Contains(secret, StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A file another process holds exclusively cannot be a file the supervision wrote.
            return false;
        }
    }
}
