namespace Core.Tests.Support.Audit;

/// <summary>
/// A folder under the system's temporary path that holds a log file like the one the database server writes, for the tests that
/// start the host and need only that the folder is there to read; it lives until the process ends. The folder is read-only, as the
/// host demands of the one it reads the server's log from.
/// </summary>
public static class ServerLogFiles
{
    private static readonly Lazy<string> SharedDirectory = new(CreateShared);

    /// <summary>The folder of the shared log file, as the host is told to read it.</summary>
    public static string Shared => SharedDirectory.Value;

    /// <summary>Takes the write permission from the folder, on the systems that have it, as the host demands of the folder of the server's log.</summary>
    public static void Seal(string directory)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        }
    }

    /// <summary>Gives the write permission back, so that the folder can be removed.</summary>
    public static void Unseal(string directory)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private static string CreateShared()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"cdg-server-log-test-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "postgresql-test.csv"), string.Empty);
        Seal(directory);
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            Unseal(directory);
            Directory.Delete(directory, recursive: true);
        };
        return directory;
    }
}
