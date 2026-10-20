namespace Core.Tests.Architecture.CodeRules;

/// <summary>
/// A copy of the repository, in a folder of the same name, built with the commands of the gate: no artifacts path, no added targets, no
/// redirected lock, the canary appended to the sources themselves. Only switches that no build file can read are added, and the
/// one property a build file could read, <c>UseSharedCompilation</c>, is on the list the build files are kept from reading.
/// </summary>
internal sealed class GateMirror : IDisposable
{
    private const string FragmentName = "Fragment.xml.txt";
    private const string BuildProps = "core/Directory.Build.props";
    private static readonly string[] SkippedDirectories = [".git", "bin", "obj", "node_modules", ".venv", ".verify-code"];

    private readonly ScratchDirectory scratch;

    private GateMirror(string repositoryRoot)
    {
        scratch = ScratchDirectory.Create();
        Root = Path.Combine(scratch.Path, Path.GetFileName(repositoryRoot));
        Copy(repositoryRoot, Root);
    }

    public string Root { get; }

    public static GateMirror Of(string repositoryRoot) => new(repositoryRoot);

    public string ProjectPath(string moduleName) => Path.Combine(Root, "core", "src", moduleName, moduleName + ".csproj");

    /// <summary>A fixture's fragment joins the build props of the core; its other files land at the same place in the copy.</summary>
    public void Apply(string fixtureDirectory)
    {
        var props = Path.Combine(Root, BuildProps);
        var text = File.ReadAllText(props);
        var end = text.LastIndexOf("</Project>", StringComparison.Ordinal);
        File.WriteAllText(props, text[..end] + File.ReadAllText(Path.Combine(fixtureDirectory, FragmentName)) + text[end..]);
        foreach (var file in Directory.EnumerateFiles(fixtureDirectory, "*", SearchOption.AllDirectories).Where(file => Path.GetFileName(file) != FragmentName))
        {
            var target = Path.Combine(Root, Path.GetRelativePath(fixtureDirectory, file));
            _ = Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    /// <summary>Appends the canary to every source of the project, builds it as the gate does and reads what the compiler was given.</summary>
    public async Task<UnseenBuild> BuildAsync(string moduleName, CancellationToken cancellationToken)
    {
        var projectPath = ProjectPath(moduleName);
        var sites = PlantCanaries(Path.GetDirectoryName(projectPath)!);
        var project = Path.GetRelativePath(Root, projectPath);
        _ = (await DotnetCommand.RunAsync(["restore", project, "--locked-mode", "-nologo", "-nodeReuse:false"], Root, cancellationToken))
            .OutputOfSuccess($"The locked restore of {moduleName} in the copy");
        var build = await DotnetCommand.RunAsync(
            ["build", project, "--no-restore", "-nologo", "-nodeReuse:false", "-p:UseSharedCompilation=false", "-v:n", "-tl:off"], Root, cancellationToken);
        var arguments = CompilerCommandLine.Of(build.Output, projectPath);
        var output = new CompilerBuildOutput(
            projectPath,
            arguments,
            Path.Combine(Path.GetDirectoryName(projectPath)!, "obj"),
            [ToolLocations.DotnetRoot, ToolLocations.GlobalPackages],
            CompilerCommandLine.IntermediateDirectory(arguments, projectPath));
        return new UnseenBuild(Root, sites, build, CompilerArguments.Read(output));
    }

    public void Dispose() => scratch.Dispose();

    private static void Copy(string source, string target)
    {
        _ = Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source).Where(file => Path.GetFileName(file) != ".git"))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.EnumerateDirectories(source).Where(directory => !SkippedDirectories.Contains(Path.GetFileName(directory), StringComparer.OrdinalIgnoreCase)))
        {
            Copy(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }

    private List<CanarySite> PlantCanaries(string projectDirectory) =>
        [.. Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(source => !IsBuildOutput(Path.GetRelativePath(projectDirectory, source)))
            .Order(StringComparer.Ordinal)
            .Select(Plant)];

    private static bool IsBuildOutput(string relativePath) =>
        Array.Exists(relativePath.Split(Path.DirectorySeparatorChar), part => SkippedDirectories.Contains(part, StringComparer.OrdinalIgnoreCase));

    private CanarySite Plant(string source, int ordinal)
    {
        var text = File.ReadAllText(source);
        var head = text.EndsWith('\n') ? text : text + Environment.NewLine;
        var offset = head.Count(character => character == '\n');
        File.WriteAllText(source, head + LoggingCanary.Text(ordinal));
        var shown = Path.GetRelativePath(Root, source).Replace(Path.DirectorySeparatorChar, '/');
        return new CanarySite(shown, shown, [.. LoggingCanary.RouteLines().Select(line => line + offset)]);
    }
}
