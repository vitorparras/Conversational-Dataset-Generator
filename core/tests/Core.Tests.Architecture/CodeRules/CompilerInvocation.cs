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

    public required IReadOnlyList<string> Analyzers { get; init; }

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
        Evaluated.GetOrAdd(projectPath, path => new Lazy<CompilerInvocation>(() => Evaluate(path))).Value;

    /// <summary>The invocation of a fixture project, built in a temporary copy of the fixture tree so that its relative includes resolve.</summary>
    public static CompilerInvocation OfFixture(string relativeDirectory, string projectName)
    {
        using var scratch = ScratchDirectory.Create();
        var tree = scratch.CopyFixture(Path.Combine(SourceTree.FixturesDirectory, relativeDirectory), "tree");
        return Evaluate(Path.Combine(tree, projectName, projectName + ".csproj"));
    }

    private static CompilerInvocation Evaluate(string projectPath)
    {
        using var scratch = ScratchDirectory.Create();
        var description = $"Reading the compiler invocation of {Path.GetFileName(projectPath)}";
        _ = Run(projectPath, scratch.Path, "-t:Restore").OutputOfSuccess(description);
        var output = Run(projectPath, scratch.Path, "-t:Build", "-p:ProvideCommandLineArgs=true", "-getItem:CscCommandLineArgs", "-getProperty:NuGetPackageRoot,MSBuildExtensionsPath,NetCoreTargetingPackRoot")
            .OutputOfSuccess(description);
        using var document = JsonDocument.Parse(output[output.IndexOf('{', StringComparison.Ordinal)..]);
        var arguments = document.RootElement.GetProperty("Items").GetProperty("CscCommandLineArgs").EnumerateArray()
            .Select(item => item.GetProperty("Identity").GetString()!);
        var roots = document.RootElement.GetProperty("Properties").EnumerateObject().Select(property => property.Value.GetString()!);
        return CompilerArguments.Read(projectPath, [.. arguments], Path.Combine(scratch.Path, "artifacts"), [.. roots]);
    }

    /// <summary>One <c>msbuild</c> run with everything it writes sent to the scratch directory, and no process left behind.</summary>
    private static CommandOutcome Run(string projectPath, string scratch, params string[] extra) =>
        DotnetCommand.RunAsync(
            [
                "msbuild", projectPath, "-nologo", "-nodeReuse:false",
                "-p:UseSharedCompilation=false",
                "-p:RestoreLockedMode=false",
                $"-p:NuGetLockFilePath={Path.Combine(scratch, "packages.lock.json")}",
                $"-p:ArtifactsPath={Path.Combine(scratch, "artifacts")}",
                .. extra,
            ],
            Path.GetDirectoryName(projectPath)!,
            CancellationToken.None).GetAwaiter().GetResult();
}
