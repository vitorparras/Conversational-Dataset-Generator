using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>Production code and the files its build generates silence no analyzer by pragma or attribute, and the command line by no option, apart from the banned-symbol note.</summary>
internal static partial class ProductionSuppressionRules
{
    public static IEnumerable<Violation> CheckSources(IReadOnlyList<SourceFile> files) =>
        files.SelectMany(Pragmas).Concat(Attributes(files));

    /// <summary>A diagnostic id of an analyzer, other than a compiler warning, that the command line gives anything but error.</summary>
    public static IEnumerable<Violation> CheckOptions(CompilerInvocation invocation) =>
        invocation.SpecificDiagnosticOptions
            .Where(option => option.Value != ReportDiagnostic.Error && !CompilerWarning().IsMatch(option.Key))
            .Select(option => new Violation(invocation.ProjectPath, 1, $"{option.Key} is given the severity {option.Value} on the compiler command line"));

    private static IEnumerable<Violation> Pragmas(SourceFile file) =>
        file.Root.DescendantNodes(descendIntoTrivia: true)
            .OfType<PragmaWarningDirectiveTriviaSyntax>()
            .Where(pragma => pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword))
            .Select(pragma => new Violation(file.Path, SourceFile.LineOf(pragma), "#pragma warning disable in production code"));

    private static IEnumerable<Violation> Attributes(IReadOnlyList<SourceFile> files)
    {
        var compilation = SourceCompilation.Of(files);
        return files
            .SelectMany(file => Suppression.FindIn(file, compilation.GetSemanticModel(file.Tree)))
            .Where(suppression => suppression.CheckId is { } id && !id.StartsWith(LoggingSuppressionRules.Diagnostic, StringComparison.Ordinal))
            .Select(suppression => new Violation(suppression.File.Path, suppression.Line, $"{suppression.CheckId} suppressed by attribute in production code"));
    }

    [GeneratedRegex(@"^(CS)?\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex CompilerWarning();
}
