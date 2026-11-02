namespace Core.Tests.Support.Audit;

/// <summary>
/// A folder under the system's temporary path that holds a log file like the one the database server writes, for the tests that
/// start the host and need only that the folder is there to read; it lives until the process ends.
/// </summary>
public static class ServerLogFiles
{
    private static readonly Lazy<string> SharedDirectory = new(CreateShared);

    /// <summary>The folder of the shared log file, as the host is told to read it.</summary>
    public static string Shared => SharedDirectory.Value;

    private static string CreateShared()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"cdg-server-log-test-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "postgresql-test.csv"), string.Empty);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Directory.Delete(directory, recursive: true);
        return directory;
    }
}
