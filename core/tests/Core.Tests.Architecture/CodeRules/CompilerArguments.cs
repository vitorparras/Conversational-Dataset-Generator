using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>Reads the arguments of a <c>csc</c> command line the way the compiler does, response files included.</summary>
internal static class CompilerArguments
{
    public static CompilerInvocation Read(string projectPath, IReadOnlyList<string> arguments, string generatedDirectory, IReadOnlyList<string> analyzerRoots)
    {
        var parsed = CSharpCommandLineParser.Default.Parse(arguments, Path.GetDirectoryName(projectPath), AppContext.BaseDirectory);
        var errors = parsed.Errors.Where(error => error.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"The compiler arguments of {Path.GetFileName(projectPath)} do not parse: {string.Join("; ", errors)}");
        }

        var sources = parsed.SourceFiles.Select(file => file.Path).Where(path => !IsUnder(path, generatedDirectory)).ToList();
        return new CompilerInvocation
        {
            ProjectPath = projectPath,
            Sources = sources,
            SourceFiles = [.. sources.Select(SourceFile.Read)],
            Analyzers = [.. parsed.AnalyzerReferences.Select(reference => reference.FilePath)],
            AdditionalFiles = [.. parsed.AdditionalFiles.Select(file => file.Path)],
            AnalyzerRoots = analyzerRoots,
            SpecificDiagnosticOptions = parsed.CompilationOptions.SpecificDiagnosticOptions,
            GeneralDiagnosticOption = parsed.CompilationOptions.GeneralDiagnosticOption,
            SkipAnalyzers = parsed.SkipAnalyzers,
            ConfigSet = AnalyzerConfigSet.Create(parsed.AnalyzerConfigPaths.Select(path => AnalyzerConfig.Parse(File.ReadAllText(path), path)).ToImmutableArray()),
        };
    }

    private static bool IsUnder(string path, string directory) =>
        path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
