namespace Core.Tests.Architecture.CodeRules;

/// <summary>Builds a real project with a canary added from outside the repository tree, leaving every tracked file and the project's own lock file untouched.</summary>
internal sealed class CanaryWorkspace : IDisposable
{
    private const string FragmentName = "Fragment.xml.txt";

    private readonly ScratchDirectory scratch;
    private readonly string projectPath;
    private readonly string canaryName = "canary-" + Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture);
    private string fragment = string.Empty;

    private CanaryWorkspace(string projectPath, ScratchDirectory scratch)
    {
        this.projectPath = projectPath;
        this.scratch = scratch;
        _ = Directory.CreateDirectory(CanaryDirectory);
    }

    private string ProjectDirectory => Path.GetDirectoryName(projectPath)!;

    private string CanaryDirectory => Path.Combine(ProjectDirectory, "obj", canaryName);

    public static CanaryWorkspace OfProject(string projectPath) => new(projectPath, ScratchDirectory.Create());

    /// <summary>A project that exists only as a fixture, built in a temporary tree that inherits the build files of the core.</summary>
    public static CanaryWorkspace OfFixtureProject(string fixtureDirectory, string projectName)
    {
        var scratch = ScratchDirectory.Create();
        var core = SourceTree.CoreRoot;
        File.WriteAllText(Path.Combine(scratch.Path, "Directory.Build.props"), ImportOf(core, "Directory.Build.props"));
        File.WriteAllText(Path.Combine(scratch.Path, "Directory.Packages.props"), ImportOf(core, "Directory.Packages.props"));
        File.Copy(Path.Combine(core, "nuget.config"), Path.Combine(scratch.Path, "nuget.config"));
        var projectDirectory = scratch.CopyFixture(fixtureDirectory, Path.Combine("src", projectName));
        return new CanaryWorkspace(Path.Combine(projectDirectory, projectName + ".csproj"), scratch);
    }

    /// <summary>Plants one canary per source the project compiles, beside the project so that the analyzer configs above it apply, and returns where each route sits.</summary>
    public IReadOnlyList<CanarySite> PlantCanaries() =>
        [.. CompilerInvocation.Of(projectPath).Sources.Select(Plant)];

    /// <summary>Applies a fixture: its fragment joins the build of the project and its other files land among the canaries.</summary>
    public void Apply(string fixtureDirectory)
    {
        var fragmentPath = Path.Combine(fixtureDirectory, FragmentName);
        fragment = File.Exists(fragmentPath) ? File.ReadAllText(fragmentPath) : string.Empty;
        foreach (var file in Directory.EnumerateFiles(fixtureDirectory).Where(file => Path.GetFileName(file) != FragmentName))
        {
            File.Copy(file, Path.Combine(CanaryDirectory, Path.GetFileName(file)), overwrite: true);
        }
    }

    public Task<CommandOutcome> BuildAsync(CancellationToken cancellationToken) =>
        DotnetCommand.RunAsync(BuildArguments(WriteBuildFiles()), ProjectDirectory, cancellationToken);

    public void Dispose()
    {
        try
        {
            ScratchDirectory.Remove(CanaryDirectory);
        }
        finally
        {
            scratch.Dispose();
        }
    }

    private static string ImportOf(string directory, string name) =>
        $"<Project><Import Project=\"{directory}{Path.DirectorySeparatorChar}{name}\" /></Project>";

    private string WriteBuildFiles()
    {
        var fragmentPath = Path.Combine(scratch.Path, CanaryTargets.FragmentFileName);
        var targetsPath = Path.Combine(scratch.Path, "canary.targets");
        var canaryFiles = Directory.EnumerateFiles(CanaryDirectory, "*.cs", SearchOption.AllDirectories);
        File.WriteAllText(fragmentPath, $"<Project>{fragment}</Project>");
        File.WriteAllText(targetsPath, CanaryTargets.Text(projectPath, canaryFiles, scratch.Path, CanaryDirectory));
        return targetsPath;
    }

    private List<string> BuildArguments(string targetsPath) =>
    [
        "build", projectPath, "-nologo", "-nodeReuse:false",
        "-p:UseSharedCompilation=false",
        "-p:RestoreLockedMode=false",
        $"-p:CustomAfterMicrosoftCommonTargets={targetsPath}",
        "--artifacts-path", Path.Combine(scratch.Path, "artifacts"),
    ];

    private CanarySite Plant(string source, int ordinal)
    {
        var directory = Path.Combine(CanaryDirectory, ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture));
        _ = Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, Path.GetFileName(source)), LoggingCanary.Text(ordinal));
        var shown = $"{canaryName}/{ordinal}/{Path.GetFileName(source)}";
        return new CanarySite(Path.GetRelativePath(SourceTree.CoreRoot, source).Replace(Path.DirectorySeparatorChar, '/'), shown, LoggingCanary.RouteLines());
    }
}
