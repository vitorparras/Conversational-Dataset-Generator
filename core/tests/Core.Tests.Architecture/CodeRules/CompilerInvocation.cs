using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.CodeAnalysis;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>What the compiler is given for one project, read from the command line its build would run: the
/// sources, the analyzers, the additional files and the diagnostic options, as the compiler itself parses
/// them.</summary>
internal sealed class CompilerInvocation
{
    private static readonly ConcurrentDictionary<string, Lazy<CompilerInvocation>> Evaluated = new(StringComparer.OrdinalIgnoreCase);

    public required string ProjectPath { get; init; }

    /// <summary>The name of the assembly the compiler writes.</summary>
    public required string AssemblyName { get; init; }

    /// <summary>The assemblies of the build's own projects that the compiler is given, by the name each one carries.</summary>
    public required IReadOnlyList<string> ProjectReferences { get; init; }

    public required IReadOnlyList<string> Sources { get; init; }

    public required IReadOnlyList<SourceFile> SourceFiles { get; init; }

    /// <summary>Sources the build compiles from under its own output folders.</summary>
    public required IReadOnlyList<SourceFile> BuildAddedFiles { get; init; }

    /// <summary>What the generators emit when run on the compilation; the path names the generator and the hint.</summary>
    public required IReadOnlyList<SourceFile> GeneratorFiles { get; init; }

    /// <summary>The folder the build writes its own files to, the root of the intermediate output.</summary>
    public required string IntermediateDirectory { get; init; }

    /// <summary>The <c>/analyzerconfig:</c> files, in the order the compiler is given them.</summary>
    public required IReadOnlyList<string> AnalyzerConfigPaths { get; init; }

    public IReadOnlyList<SourceFile> GeneratedFiles => [.. BuildAddedFiles, .. GeneratorFiles];

    public required IReadOnlyList<string> Analyzers { get; init; }

    /// <summary>Everything that kept an analyzer or a generator from being inspected; any of it fails the rules closed.</summary>
    public required IReadOnlyList<string> AnalyzerFailures { get; init; }

    /// <summary>Every <c>DiagnosticSuppressor</c> among the analyzers, with the ids it can suppress.</summary>
    public required IReadOnlyList<AnalyzerSuppressor> Suppressors { get; init; }

    public required IReadOnlyList<string> AdditionalFiles { get; init; }

    /// <summary>The diagnostic ids each analyzer assembly can report, by the path the compiler loads it from.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> DiagnosticsByAnalyzer { get; init; } = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>The package each analyzer from the package cache belongs to, as the cache recorded it when the package was extracted.</summary>
    public IReadOnlyDictionary<string, PackageIdentity> AnalyzerPackages { get; init; } = new Dictionary<string, PackageIdentity>();

    /// <summary>The preprocessor symbols the sources are parsed with.</summary>
    public IReadOnlyList<string> PreprocessorSymbols { get; init; } = [];

    /// <summary>The folders analyzers may come from: the SDK, its targeting packs and the package cache.</summary>
    public required IReadOnlyList<string> AnalyzerRoots { get; init; }

    /// <summary>The severity each <c>/nowarn</c>, <c>/warnaserror:</c> and <c>/warnaserror-:</c> gives a diagnostic id.</summary>
    public required ImmutableDictionary<string, ReportDiagnostic> SpecificDiagnosticOptions { get; init; }

    /// <summary>What <c>/warnaserror</c> and <c>/warnaserror-</c> do to the warnings that no id names.</summary>
    public required ReportDiagnostic GeneralDiagnosticOption { get; init; }

    /// <summary>The rule set the compiler was pointed at, as its parser read it, whether it came on the command line or from a response file.</summary>
    public string? RuleSetPath { get; init; }

    /// <summary>True when the build runs no analyzer at all.</summary>
    public required bool SkipAnalyzers { get; init; }

    /// <summary>The configs merged the way the compiler merges them, kept as read while the build output still existed.</summary>
    public AnalyzerConfigSet ConfigSet { get; init; } = AnalyzerConfigSet.Create(Array.Empty<AnalyzerConfig>());

    /// <summary>The invocation of a project as it sits in the repository; nothing is written under the repository.</summary>
    public static CompilerInvocation Of(string projectPath) =>
        Evaluated.GetOrAdd(projectPath, path => new Lazy<CompilerInvocation>(() => Evaluate(path, null))).Value;

    /// <summary>The invocation of a fixture project, built in a temporary copy of the fixture tree with its own package cache, so that nothing reaches the machine's.</summary>
    public static CompilerInvocation OfFixture(string relativeDirectory, string projectName)
    {
        using var scratch = ScratchDirectory.Create();
        var tree = scratch.CopyFixture(Path.Combine(SourceTree.FixturesDirectory, relativeDirectory), "tree");
        FixturePackages.Build(tree);
        return Evaluate(Path.Combine(tree, projectName, projectName + ".csproj"), Path.Combine(scratch.Path, "packages"));
    }

    private static CompilerInvocation Evaluate(string projectPath, string? packages)
    {
        using var scratch = ScratchDirectory.Create();
        var run = new MsBuildRun(projectPath, scratch.Path, packages);
        var description = $"Reading the compiler invocation of {Path.GetFileName(projectPath)}";
        _ = run.Execute("-t:Restore").OutputOfSuccess(description);
        var output = run.Execute("-t:Build", "-p:ProvideCommandLineArgs=true", "-getItem:CscCommandLineArgs", "-getProperty:IntermediateOutputPath")
            .OutputOfSuccess(description);
        using var document = JsonDocument.Parse(output[output.IndexOf('{', StringComparison.Ordinal)..]);
        var arguments = document.RootElement.GetProperty("Items").GetProperty("CscCommandLineArgs").EnumerateArray()
            .Select(item => item.GetProperty("Identity").GetString()!);
        var intermediate = Path.GetFullPath(document.RootElement.GetProperty("Properties").GetProperty("IntermediateOutputPath").GetString()!, Path.GetDirectoryName(projectPath)!);
        return CompilerArguments.Read(new CompilerBuildOutput(projectPath, [.. arguments], run.Artifacts, [ToolLocations.DotnetRoot, packages ?? ToolLocations.GlobalPackages], intermediate));
    }
}
