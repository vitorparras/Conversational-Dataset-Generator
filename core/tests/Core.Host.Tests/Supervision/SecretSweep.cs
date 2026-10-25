using System.IO.Enumeration;

namespace Core.Host.Tests.Supervision;

/// <summary>Reads the places a secret could have been left and names the files that hold it.</summary>
internal static class SecretSweep
{
    private const long LargestFile = 8 * 1024 * 1024;
    private const string SharedMemory = "/dev/shm";
    private const string VirtualEnvironment = ".venv";

    /// <summary>
    /// The files holding the secret in the installation, in the language service project, in the
    /// temporary folder, in the user's folder and in shared memory; the last three are shared with the
    /// whole machine, so only what was written since the given time is read there. Hidden files and
    /// folders are read like any other.
    /// </summary>
    public static IReadOnlyList<string> FilesHolding(string secret, DateTimeOffset since)
    {
        string[] shared = [Path.GetTempPath(), Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), SharedMemory];
        return
        [
            .. Holding(AppContext.BaseDirectory, secret, since: null),
            .. Holding(LanguageServiceInterpreter.ProjectFolder(), secret, since: null),
            .. Outermost(shared.Where(Directory.Exists)).SelectMany(folder => Holding(folder, secret, since)),
        ];
    }

    /// <summary>The folders no other of them contains, so a file below two of them is read once, not twice.</summary>
    private static IEnumerable<string> Outermost(IEnumerable<string> folders)
    {
        var full = folders.Select(folder => Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder))).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return full.Where(folder => !full.Exists(other => other != folder && folder.StartsWith(other + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>The files below the folder that hold the secret; the virtual environment is a copy of packages, not something the supervision writes.</summary>
    public static IReadOnlyList<string> Holding(string folder, string secret, DateTimeOffset? since)
    {
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 };
        var files = new FileSystemEnumerable<string>(folder, (ref entry) => entry.ToFullPath(), options)
        {
            ShouldIncludePredicate = (ref entry) => !entry.IsDirectory && IsReadable(ref entry, since),
            ShouldRecursePredicate = (ref entry) =>
                !entry.Attributes.HasFlag(FileAttributes.ReparsePoint) && !entry.FileName.SequenceEqual(VirtualEnvironment),
        };
        return [.. files.Distinct().Where(path => Holds(path, secret))];
    }

    private static bool IsReadable(ref FileSystemEntry entry, DateTimeOffset? since) =>
        entry.Length is > 0 and <= LargestFile && (since is null || entry.LastWriteTimeUtc >= since);

    private static bool Holds(string path, string secret)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Contains(secret, StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A file another process holds exclusively is not one the supervision wrote.
            return false;
        }
    }
}
