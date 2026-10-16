namespace Core.Tests.Architecture.CodeRules;

/// <summary>Finds the production sources and the fixtures from wherever the tests run.</summary>
internal static class SourceTree
{
    private static readonly Lazy<string> CoreDirectory = new(FindCoreDirectory);
    private static readonly Lazy<IReadOnlyList<SourceFile>> Production = new(ReadProductionFiles);

    public static string CoreRoot => CoreDirectory.Value;

    public static string FixturesDirectory => Path.Combine(CoreDirectory.Value, "tests", "fixtures", "code-rules");

    public static IReadOnlyList<SourceFile> ProductionFiles() => Production.Value;

    public static IReadOnlyList<ProjectFile> ProductionProjects() =>
        [.. EnumerateSources(Path.Combine(CoreDirectory.Value, "src"), "*.csproj").Select(ProjectFile.Read)];

    public static ProjectFile ProductionProject(string moduleName) =>
        ProductionProjects().Single(project => project.ModuleName == moduleName);

    public static SourceFile Fixture(string relativePath) =>
        SourceFile.Read(Path.Combine(FixturesDirectory, relativePath));

    public static string FixtureText(string relativePath) =>
        File.ReadAllText(Path.Combine(FixturesDirectory, relativePath));

    /// <summary>A fixture project is stored as <c>.csproj.txt</c> so that no build tool picks it up.</summary>
    public static ProjectFile FixtureProject(string relativePath)
    {
        var path = Path.Combine(FixturesDirectory, relativePath);
        return ProjectFile.FromText(path[..^".txt".Length], File.ReadAllText(path));
    }

    public static IReadOnlyList<SourceFile> FixtureFiles(string relativeDirectory) =>
        [.. EnumerateSources(Path.Combine(FixturesDirectory, relativeDirectory), "*.cs").Select(SourceFile.Read)];

    private static IReadOnlyList<SourceFile> ReadProductionFiles() =>
        [.. ProductionProjects().SelectMany(project => CompilerInvocation.Of(project.Path).SourceFiles).DistinctBy(file => file.Path)];

    private static IEnumerable<string> EnumerateSources(string directory, string pattern) =>
        Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .OrderBy(path => path, StringComparer.Ordinal);

    private static bool IsBuildOutput(string path) =>
        Parts(path).Contains("bin", StringComparer.Ordinal) || Parts(path).Contains("obj", StringComparer.Ordinal);

    /// <summary>The segments below the core directory, so that where the repository was cloned never changes the verdict.</summary>
    private static string[] Parts(string path) =>
        Path.GetRelativePath(CoreDirectory.Value, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

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
