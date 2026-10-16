using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>Loads the analyzers a build is given and asks each one what it does, wherever it came from.</summary>
internal static class AnalyzerInspector
{
    public static IReadOnlyList<AnalyzerSuppressor> FindSuppressors(IEnumerable<string> analyzerPaths)
    {
        var loader = new AnalyzerLoader();
        try
        {
            return [.. analyzerPaths.SelectMany(path => SuppressorsIn(path, loader))];
        }
        finally
        {
            loader.Unload();
        }
    }

    private static IEnumerable<AnalyzerSuppressor> SuppressorsIn(string path, AnalyzerLoader loader) =>
        new AnalyzerFileReference(path, loader).GetAnalyzers(LanguageNames.CSharp)
            .OfType<DiagnosticSuppressor>()
            .Select(suppressor => new AnalyzerSuppressor(
                path, suppressor.GetType().FullName ?? suppressor.GetType().Name, [.. suppressor.SupportedSuppressions.Select(rule => rule.SuppressedDiagnosticId)]));
}
