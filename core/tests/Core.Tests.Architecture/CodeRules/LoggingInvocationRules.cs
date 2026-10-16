using Microsoft.CodeAnalysis;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>What the compiler is given must keep the banned-symbol diagnostic an error on every source, with its analyzer and its list of symbols loaded.</summary>
internal static class LoggingInvocationRules
{
    private const string AnalyzerPackage = "BannedApiAnalyzers";
    private const string SymbolsFile = "BannedSymbols.txt";

    public static IEnumerable<Violation> Check(CompilerInvocation invocation) =>
        CheckNotIgnored(invocation)
            .Concat(CheckWarningsAreErrors(invocation))
            .Concat(CheckAnalyzerAndSymbolsLoaded(invocation))
            .Concat(CheckSeverityOnEverySource(invocation));

    public static IEnumerable<Violation> CheckNotIgnored(CompilerInvocation invocation) =>
        invocation.NoWarn
            .Where(id => id.Equals(LoggingSuppressionRules.Diagnostic, StringComparison.OrdinalIgnoreCase))
            .Select(_ => At(invocation, $"{LoggingSuppressionRules.Diagnostic} is on the list of warnings the compiler ignores"));

    public static IEnumerable<Violation> CheckWarningsAreErrors(CompilerInvocation invocation) =>
        invocation.WarningsAsErrors ? [] : [At(invocation, "the compiler is not told to treat warnings as errors")];

    public static IEnumerable<Violation> CheckAnalyzerAndSymbolsLoaded(CompilerInvocation invocation)
    {
        if (!invocation.Analyzers.Any(path => path.Contains(AnalyzerPackage, StringComparison.Ordinal)))
        {
            yield return At(invocation, $"the {AnalyzerPackage} analyzer is not given to the compiler");
        }

        if (!invocation.AdditionalFiles.Any(path => Path.GetFileName(path) == SymbolsFile))
        {
            yield return At(invocation, $"{SymbolsFile} is not given to the compiler");
        }
    }

    /// <summary>Each source, judged by the severity the compiler would give the diagnostic there once every config is merged; no entry means the analyzer's own error.</summary>
    public static IEnumerable<Violation> CheckSeverityOnEverySource(CompilerInvocation invocation) =>
        invocation.Sources
            .Select(source => (Source: source, Severity: SeverityAt(invocation, source)))
            .Where(entry => entry.Severity is not null and not ReportDiagnostic.Error)
            .Select(entry => new Violation(entry.Source, 1, $"{LoggingSuppressionRules.Diagnostic} is {entry.Severity} here; only error is allowed"));

    private static ReportDiagnostic? SeverityAt(CompilerInvocation invocation, string source) =>
        new[] { invocation.ConfigSet.GetOptionsForSourcePath(source), invocation.ConfigSet.GlobalConfigOptions }
            .Select(options => options.TreeOptions.TryGetValue(LoggingSuppressionRules.Diagnostic, out var severity) ? (ReportDiagnostic?)severity : null)
            .FirstOrDefault(severity => severity is not null);

    private static Violation At(CompilerInvocation invocation, string message) => new(invocation.ProjectPath, 1, message);
}
