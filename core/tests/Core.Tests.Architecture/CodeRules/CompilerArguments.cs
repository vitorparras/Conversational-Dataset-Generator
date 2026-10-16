using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>Reads the arguments of a <c>csc</c> command line the way the compiler does, response files included.</summary>
internal static class CompilerArguments
{
    public static CompilerInvocation Read(CompilerBuildOutput build)
    {
        var projectDirectory = Path.GetDirectoryName(build.ProjectPath)!;
        var parsed = CSharpCommandLineParser.Default.Parse(build.Arguments, projectDirectory, AppContext.BaseDirectory);
        var errors = parsed.Errors.Where(error => error.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"The compiler arguments of {Path.GetFileName(build.ProjectPath)} do not parse: {string.Join("; ", errors)}");
        }

        var compiled = parsed.SourceFiles.Select(file => file.Path).ToList();
        var sources = compiled.Where(path => !IsUnder(path, build.ArtifactsDirectory)).ToList();
        var analyzers = parsed.AnalyzerReferences.Select(reference => Path.GetFullPath(reference.FilePath, projectDirectory)).ToList();
        return new CompilerInvocation
        {
            ProjectPath = build.ProjectPath,
            Sources = sources,
            SourceFiles = [.. sources.Select(SourceFile.Read)],
            GeneratedFiles = [.. compiled.Except(sources).Concat(GeneratedBy(build)).Select(SourceFile.Read)],
            Analyzers = analyzers,
            Suppressors = AnalyzerInspector.FindSuppressors(analyzers),
            AdditionalFiles = [.. parsed.AdditionalFiles.Select(file => file.Path)],
            AnalyzerRoots = build.AnalyzerRoots,
            SpecificDiagnosticOptions = parsed.CompilationOptions.SpecificDiagnosticOptions,
            GeneralDiagnosticOption = parsed.CompilationOptions.GeneralDiagnosticOption,
            SkipAnalyzers = parsed.SkipAnalyzers,
            ConfigSet = AnalyzerConfigSet.Create(parsed.AnalyzerConfigPaths.Select(path => AnalyzerConfig.Parse(File.ReadAllText(path), path)).ToImmutableArray()),
        };
    }

    private static IEnumerable<string> GeneratedBy(CompilerBuildOutput build) =>
        Directory.Exists(build.GeneratedFilesDirectory)
            ? Directory.EnumerateFiles(build.GeneratedFilesDirectory, "*.cs", SearchOption.AllDirectories)
            : [];

    private static bool IsUnder(string path, string directory) =>
        path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
