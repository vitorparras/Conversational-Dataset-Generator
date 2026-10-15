namespace Core.Tests.Architecture.CodeRules;

/// <summary>Finds the production sources and the fixtures from wherever the tests run.</summary>
internal static class SourceTree
{
    private static readonly Lazy<string> CoreDirectory = new(FindCoreDirectory);
    private static readonly string[] AnalyzerConfigNames = [".editorconfig", ".globalconfig"];

    public static string FixturesDirectory => Path.Combine(CoreDirectory.Value, "tests", "fixtures", "code-rules");

    public static IReadOnlyList<SourceFile> ProductionFiles() =>
        [.. EnumerateSources(Path.Combine(CoreDirectory.Value, "src"), "*.cs").Select(SourceFile.Read)];

    public static IReadOnlyList<ProjectFile> ProductionProjects() =>
        [.. EnumerateSources(Path.Combine(CoreDirectory.Value, "src"), "*.csproj").Select(ProjectFile.Read)];

    /// <summary>Every MSBuild file the production projects evaluate: their own files and the shared props and targets above them.</summary>
    public static IReadOnlyList<ProjectFile> ProductionBuildFiles() =>
        [.. EnumerateSources(Path.Combine(CoreDirectory.Value, "src"), "*.csproj")
            .Concat(EnumerateSources(CoreDirectory.Value, "*.props").Where(IsOutsideTests))
            .Concat(EnumerateSources(CoreDirectory.Value, "*.targets").Where(IsOutsideTests))
            .Select(ProjectFile.Read)];

    /// <summary>Every analyzer config the production projects read: under the core, outside the tests, plus the repository root.</summary>
    public static IReadOnlyList<AnalyzerConfig> ProductionAnalyzerConfigs() =>
        [.. EnumerateSources(CoreDirectory.Value, ".editorconfig")
            .Concat(EnumerateSources(CoreDirectory.Value, ".globalconfig"))
            .Where(IsOutsideTests)
            .Concat(RepositoryConfigs())
            .Select(AnalyzerConfig.Read)];

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

    private static IEnumerable<string> EnumerateSources(string directory, string pattern) =>
        Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .OrderBy(path => path, StringComparer.Ordinal);

    private static IEnumerable<string> RepositoryConfigs() =>
        AnalyzerConfigNames
            .Select(name => Path.Combine(Path.GetDirectoryName(CoreDirectory.Value)!, name))
            .Where(File.Exists);

    private static bool IsOutsideTests(string path) =>
        !Parts(path).Contains("tests", StringComparer.Ordinal);

    private static bool IsBuildOutput(string path) =>
        Parts(path).Contains("bin", StringComparer.Ordinal) || Parts(path).Contains("obj", StringComparer.Ordinal);

    private static string[] Parts(string path) => path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

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
