namespace Core.Tests.Architecture.CodeRules;

/// <summary>A throwaway copy of the production sources and the build files above them, so that a build can be run against a changed project.</summary>
internal sealed class CanaryWorkspace : IDisposable
{
    private const string FragmentName = "Fragment.xml.txt";
    private static readonly string[] RepositoryFiles = [".editorconfig", ".globalconfig", "global.json"];

    private readonly string root = Path.Combine(Path.GetTempPath(), "canary-" + Guid.NewGuid().ToString("N"));
    private readonly string project;

    public CanaryWorkspace(string projectName)
    {
        project = projectName;
        CopyTree(SourceTree.CoreRoot, Path.Combine(root, "core"));
        CopyFiles(Path.GetDirectoryName(SourceTree.CoreRoot)!, root, RepositoryFiles);
    }

    private string SourceRoot => Path.Combine(root, "core", "src");

    private string ProjectDirectory => Path.Combine(SourceRoot, project);

    private string ProjectFile => Path.Combine(ProjectDirectory, project + ".csproj");

    /// <summary>Appends the canary to every source file of the project and returns where each route now sits.</summary>
    public IReadOnlyList<CanarySite> PlantCanaries()
    {
        var files = Directory.EnumerateFiles(ProjectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Order(StringComparer.Ordinal);
        return [.. files.Select(Plant)];
    }

    /// <summary>Applies a fixture: its fragment goes into the project file and its other files land beside the project.</summary>
    public void Apply(string fixtureDirectory)
    {
        var fragment = File.ReadAllText(Path.Combine(fixtureDirectory, FragmentName));
        var text = File.ReadAllText(ProjectFile);
        File.WriteAllText(ProjectFile, text.Replace("</Project>", fragment + "</Project>", StringComparison.Ordinal));
        foreach (var file in Directory.EnumerateFiles(fixtureDirectory).Where(file => Path.GetFileName(file) != FragmentName))
        {
            File.Copy(file, Path.Combine(ProjectDirectory, Path.GetFileName(file)), overwrite: true);
        }
    }

    public Task<BuildOutcome> BuildAsync(CancellationToken cancellationToken) => BuildProcess.RunAsync(ProjectFile, cancellationToken);

    public void Dispose()
    {
        try
        {
            Directory.Delete(root, recursive: true);
        }
        catch (IOException)
        {
            // The build that used the copy has finished; a leftover temporary directory does no harm.
        }
    }

    private static bool IsBuildOutput(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(segment => segment is "bin" or "obj");

    private static void CopyFiles(string source, string target, IEnumerable<string> names)
    {
        _ = Directory.CreateDirectory(target);
        foreach (var name in names.Where(name => File.Exists(Path.Combine(source, name))))
        {
            File.Copy(Path.Combine(source, name), Path.Combine(target, name), overwrite: true);
        }
    }

    private static void CopyTree(string source, string target)
    {
        CopyFiles(source, target, Directory.EnumerateFiles(source).Select(Path.GetFileName).OfType<string>());
        foreach (var directory in Directory.EnumerateDirectories(source).Where(IsCopied))
        {
            CopyTree(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }

    private static bool IsCopied(string directory) => Path.GetFileName(directory) is not ("bin" or "obj" or "tests");

    private CanarySite Plant(string path, int ordinal)
    {
        var relative = Path.GetRelativePath(SourceRoot, path).Replace('\\', '/');
        var site = LoggingCanary.AppendTo(relative, File.ReadAllText(path), ordinal);
        File.WriteAllText(path, site.Text);
        return site;
    }
}
