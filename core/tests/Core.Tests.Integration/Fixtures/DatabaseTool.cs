namespace Core.Tests.Integration.Fixtures;

/// <summary>The files of tools/database, found above the test binaries wherever the repository was cloned.</summary>
internal static class DatabaseTool
{
    private static readonly Lazy<string> Folder = new(Find);

    public static string Directory => Folder.Value;

    public static string ReadText(string relativePath) => File.ReadAllText(Path.Combine(Folder.Value, relativePath));

    public static IReadOnlyList<string> ReadLines(string relativePath) => File.ReadAllLines(Path.Combine(Folder.Value, relativePath));

    private static string Find()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "tools", "database", "Dockerfile")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(
            (directory ?? throw new DirectoryNotFoundException("tools/database was not found above the test binaries.")).FullName,
            "tools",
            "database");
    }
}
