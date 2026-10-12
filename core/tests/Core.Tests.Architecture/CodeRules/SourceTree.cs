namespace Core.Tests.Architecture.CodeRules;

/// <summary>Finds the production sources and the fixtures from wherever the tests run.</summary>
internal static class SourceTree
{
    private static readonly Lazy<string> CoreDirectory = new(FindCoreDirectory);

    public static string FixturesDirectory => Path.Combine(CoreDirectory.Value, "tests", "fixtures", "code-rules");

    public static IReadOnlyList<SourceFile> ProductionFiles() =>
        [.. EnumerateSources(Path.Combine(CoreDirectory.Value, "src"), "*.cs").Select(SourceFile.Read)];

    public static IReadOnlyList<ProjectFile> ProductionProjects() =>
        [.. EnumerateSources(Path.Combine(CoreDirectory.Value, "src"), "*.csproj").Select(ProjectFile.Read)];

    public static SourceFile Fixture(string relativePath) =>
        SourceFile.Read(Path.Combine(FixturesDirectory, relativePath));

    public static string FixtureText(string relativePath) =>
        File.ReadAllText(Path.Combine(FixturesDirectory, relativePath));

    private static IEnumerable<string> EnumerateSources(string directory, string pattern) =>
        Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .OrderBy(path => path, StringComparer.Ordinal);

    private static bool IsBuildOutput(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Contains("bin", StringComparer.Ordinal) || parts.Contains("obj", StringComparer.Ordinal);
    }

    private static string FindCoreDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Core.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Core.slnx was not found above the test output.");
    }
}
