using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.CodeAnalysis;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>What the compiler is given for one project, read from the command line its build would run: the sources, the analyzers, the additional files and the diagnostic options, as the compiler itself parses them.</summary>
internal sealed class CompilerInvocation
{
    private static readonly ConcurrentDictionary<string, Lazy<CompilerInvocation>> Evaluated = new(StringComparer.OrdinalIgnoreCase);

    public required string ProjectPath { get; init; }

    public required IReadOnlyList<string> Sources { get; init; }

    public required IReadOnlyList<SourceFile> SourceFiles { get; init; }

    /// <summary>Sources the build produced for itself: what generators emitted and what lies under the build's own output folders.</summary>
    public required IReadOnlyList<SourceFile> GeneratedFiles { get; init; }

    public required IReadOnlyList<string> Analyzers { get; init; }

    /// <summary>Every <c>DiagnosticSuppressor</c> among the analyzers, with the ids it can suppress.</summary>
    public required IReadOnlyList<AnalyzerSuppressor> Suppressors { get; init; }

    public required IReadOnlyList<string> AdditionalFiles { get; init; }

    /// <summary>The folders analyzers may come from: the SDK, its targeting packs and the package cache.</summary>
    public required IReadOnlyList<string> AnalyzerRoots { get; init; }

    /// <summary>The severity each <c>/nowarn</c>, <c>/warnaserror:</c> and <c>/warnaserror-:</c> gives a diagnostic id.</summary>
    public required ImmutableDictionary<string, ReportDiagnostic> SpecificDiagnosticOptions { get; init; }

    /// <summary>What <c>/warnaserror</c> and <c>/warnaserror-</c> do to the warnings that no id names.</summary>
    public required ReportDiagnostic GeneralDiagnosticOption { get; init; }

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
        var output = run.Execute("-t:Build", "-p:ProvideCommandLineArgs=true", "-getItem:CscCommandLineArgs", "-p:EmitCompilerGeneratedFiles=true", $"-p:CompilerGeneratedFilesOutputPath={run.GeneratedFiles}")
            .OutputOfSuccess(description);
        using var document = JsonDocument.Parse(output[output.IndexOf('{', StringComparison.Ordinal)..]);
        var arguments = document.RootElement.GetProperty("Items").GetProperty("CscCommandLineArgs").EnumerateArray()
            .Select(item => item.GetProperty("Identity").GetString()!);
        return CompilerArguments.Read(new CompilerBuildOutput(projectPath, [.. arguments], run.Artifacts, run.GeneratedFiles, [ToolLocations.DotnetRoot, packages ?? ToolLocations.GlobalPackages]));
    }
}
