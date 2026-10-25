using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>Loads the analyzers a build is given, wherever they came from, and asks which diagnostics they can suppress and what their generators emit, failing on any that cannot be asked.</summary>
internal static class AnalyzerInspector
{
    public static AnalyzerFindings Inspect(IEnumerable<string> analyzerPaths, GeneratorInput input)
    {
        var loader = new AnalyzerLoader();
        var failures = new List<string>();
        try
        {
            var references = analyzerPaths.Select(path => Reference(path, loader, failures.Add)).ToList();
            var suppressors = references.SelectMany(SuppressorsIn).ToList();
            var generators = references.SelectMany(reference => reference.GetGenerators(LanguageNames.CSharp)).ToImmutableArray();
            var diagnostics = references
                .DistinctBy(reference => reference.FullPath, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(reference => reference.FullPath ?? string.Empty, ReportedIds, StringComparer.OrdinalIgnoreCase);
            return new AnalyzerFindings(suppressors, Generate(generators, input, failures), failures, diagnostics);
        }
        finally
        {
            loader.Unload();
        }
    }

    private static AnalyzerFileReference Reference(string path, IAnalyzerAssemblyLoader loader, Action<string> fail)
    {
        var reference = new AnalyzerFileReference(path, loader);
        reference.AnalyzerLoadFailed += (_, failure) =>
        {
            if (failure.ErrorCode is not (AnalyzerLoadFailureEventArgs.FailureErrorCode.NoAnalyzers or AnalyzerLoadFailureEventArgs.FailureErrorCode.ReferencesFramework))
            {
                fail($"{Path.GetFileName(path)} could not be loaded: {failure.Message}");
            }
        };
        return reference;
    }

    private static IReadOnlyList<string> ReportedIds(AnalyzerReference reference) =>
        [.. reference.GetAnalyzers(LanguageNames.CSharp)
            .Where(analyzer => analyzer is not DiagnosticSuppressor)
            .SelectMany(analyzer => analyzer.SupportedDiagnostics)
            .Select(descriptor => descriptor.Id)
            .Distinct(StringComparer.Ordinal)];

    private static IEnumerable<AnalyzerSuppressor> SuppressorsIn(AnalyzerReference reference) =>
        reference.GetAnalyzers(LanguageNames.CSharp)
            .OfType<DiagnosticSuppressor>()
            .Select(suppressor => new AnalyzerSuppressor(
                reference.FullPath ?? string.Empty, suppressor.GetType().FullName ?? suppressor.GetType().Name, [.. suppressor.SupportedSuppressions.Select(rule => rule.SuppressedDiagnosticId)]));

    private static List<SourceFile> Generate(ImmutableArray<ISourceGenerator> generators, GeneratorInput input, List<string> failures)
    {
        if (generators.IsEmpty)
        {
            return [];
        }

        var driver = CSharpGeneratorDriver.Create(
            generators,
            input.AdditionalFiles.Select(path => (AdditionalText)new FileText(path)),
            input.ParseOptions,
            new ConfigOptionsProvider(input.Configs));
        var result = driver.RunGenerators(input.Compilation).GetRunResult();
        failures.AddRange(result.Results.Where(run => run.Exception is not null).Select(run => $"generator {run.Generator.GetType().Name} failed: {run.Exception!.Message}"));
        return [.. result.GeneratedTrees.Select(tree => SourceFile.FromText(tree.FilePath, tree.GetText().ToString()))];
    }
}
