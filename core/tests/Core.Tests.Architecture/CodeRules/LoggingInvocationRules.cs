using Microsoft.CodeAnalysis;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>What the compiler is given must keep the banned-symbol diagnostic an error on every source, with its analyzer and its list of symbols loaded and nothing else in their place.</summary>
internal static class LoggingInvocationRules
{
    private const string AnalyzerPackage = "BannedApiAnalyzers";
    private const string SymbolsFile = "BannedSymbols.txt";

    public static IEnumerable<Violation> Check(CompilerInvocation invocation) =>
        CheckDiagnosticOptions(invocation)
            .Concat(CheckAnalyzers(invocation))
            .Concat(CheckAdditionalFiles(invocation))
            .Concat(CheckSeverityOnEverySource(invocation))
            .Concat(LoggingSuppressionRules.Check(invocation.SourceFiles, invocation.GeneratedFiles));

    /// <summary>The diagnostic is not named by a <c>/nowarn</c> or a <c>/warnaserror-:</c>, warnings are errors, and analyzers run.</summary>
    public static IEnumerable<Violation> CheckDiagnosticOptions(CompilerInvocation invocation)
    {
        if (invocation.SpecificDiagnosticOptions.TryGetValue(LoggingSuppressionRules.Diagnostic, out var named) && named != ReportDiagnostic.Error)
        {
            yield return At(invocation, $"{LoggingSuppressionRules.Diagnostic} is given the severity {named} on the command line");
        }

        if (invocation.GeneralDiagnosticOption != ReportDiagnostic.Error)
        {
            yield return At(invocation, "the compiler is not told to treat warnings as errors");
        }

        if (invocation.SkipAnalyzers)
        {
            yield return At(invocation, "the build runs no analyzer");
        }
    }

    /// <summary>The banned-symbol analyzer is loaded, no analyzer is able to suppress it, and every analyzer comes from the SDK or the package cache.</summary>
    public static IEnumerable<Violation> CheckAnalyzers(CompilerInvocation invocation)
    {
        if (!invocation.Analyzers.Any(path => path.Contains(AnalyzerPackage, StringComparison.Ordinal)))
        {
            yield return At(invocation, $"the {AnalyzerPackage} analyzer is not given to the compiler");
        }

        foreach (var failure in invocation.AnalyzerFailures)
        {
            yield return At(invocation, failure);
        }

        foreach (var suppressor in invocation.Suppressors.Where(suppressor => suppressor.Suppresses(LoggingSuppressionRules.Diagnostic)))
        {
            yield return At(invocation, $"{suppressor.TypeName} in {Path.GetFileName(suppressor.AnalyzerPath)} is able to suppress {LoggingSuppressionRules.Diagnostic}");
        }

        foreach (var analyzer in invocation.Analyzers.Where(path => !IsUnderAnyRoot(path, invocation.AnalyzerRoots)))
        {
            yield return At(invocation, $"analyzer {Path.GetFileName(analyzer)} comes from outside the SDK and the package cache");
        }
    }

    /// <summary>The only additional file is the list of banned symbols; any other would be handed to the generators.</summary>
    public static IEnumerable<Violation> CheckAdditionalFiles(CompilerInvocation invocation)
    {
        if (!invocation.AdditionalFiles.Any(path => Path.GetFileName(path) == SymbolsFile))
        {
            yield return At(invocation, $"{SymbolsFile} is not given to the compiler");
        }

        foreach (var file in invocation.AdditionalFiles.Where(path => Path.GetFileName(path) != SymbolsFile))
        {
            yield return At(invocation, $"additional file {Path.GetFileName(file)} is given to the compiler besides {SymbolsFile}");
        }
    }

    /// <summary>Each source, judged by the severity the compiler would give the diagnostic there once every config is merged; no entry means the analyzer's own error.</summary>
    public static IEnumerable<Violation> CheckSeverityOnEverySource(CompilerInvocation invocation) =>
        invocation.Sources
            .Select(source => (Source: source, Severity: SeverityAt(invocation, source)))
            .Where(entry => entry.Severity is not null and not ReportDiagnostic.Error)
            .Select(entry => new Violation(entry.Source, 1, $"{LoggingSuppressionRules.Diagnostic} is {entry.Severity} here; only error is allowed"));

    private static bool IsUnderAnyRoot(string path, IReadOnlyList<string> roots) =>
        roots.Any(root => Path.GetFullPath(path).StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase));

    private static ReportDiagnostic? SeverityAt(CompilerInvocation invocation, string source) =>
        new[] { invocation.ConfigSet.GetOptionsForSourcePath(source), invocation.ConfigSet.GlobalConfigOptions }
            .Select(options => options.TreeOptions.TryGetValue(LoggingSuppressionRules.Diagnostic, out var severity) ? (ReportDiagnostic?)severity : null)
            .FirstOrDefault(severity => severity is not null);

    private static Violation At(CompilerInvocation invocation, string message) => new(invocation.ProjectPath, 1, message);
}
