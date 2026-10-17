using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>Loads the analyzers a build is given, wherever they came from, and asks them what they do: which diagnostics they can suppress and what their generators emit. Anything that cannot be asked is a failure.</summary>
internal static class AnalyzerInspector
{
    public static AnalyzerFindings Inspect(IReadOnlyList<string> analyzerPaths, GeneratorInput input)
    {
        var loader = new AnalyzerLoader();
        var failures = new List<string>();
        try
        {
            var references = analyzerPaths.Select(path => Reference(path, loader, failures)).ToList();
            var suppressors = references.SelectMany(SuppressorsIn).ToList();
            var generators = references.SelectMany(reference => reference.GetGenerators(LanguageNames.CSharp)).ToImmutableArray();
            return new AnalyzerFindings(suppressors, Generate(generators, input, failures), failures);
        }
        finally
        {
            loader.Unload();
        }
    }

    private static AnalyzerFileReference Reference(string path, AnalyzerLoader loader, List<string> failures)
    {
        var reference = new AnalyzerFileReference(path, loader);
        reference.AnalyzerLoadFailed += (_, failure) =>
        {
            if (failure.ErrorCode is not (AnalyzerLoadFailureEventArgs.FailureErrorCode.NoAnalyzers or AnalyzerLoadFailureEventArgs.FailureErrorCode.ReferencesFramework))
            {
                failures.Add($"{Path.GetFileName(path)} could not be loaded: {failure.Message}");
            }
        };
        return reference;
    }

    private static IEnumerable<AnalyzerSuppressor> SuppressorsIn(AnalyzerFileReference reference) =>
        reference.GetAnalyzers(LanguageNames.CSharp)
            .OfType<DiagnosticSuppressor>()
            .Select(suppressor => new AnalyzerSuppressor(
                reference.FullPath, suppressor.GetType().FullName ?? suppressor.GetType().Name, [.. suppressor.SupportedSuppressions.Select(rule => rule.SuppressedDiagnosticId)]));

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
