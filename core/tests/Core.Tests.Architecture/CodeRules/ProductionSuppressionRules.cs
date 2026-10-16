using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>No Sonar rule is silenced in production: not by a pragma, not by a suppression attribute, not by the compiler switches.
/// The banned-symbol note is judged by <see cref="LoggingSuppressionRules"/>.</summary>
internal static partial class ProductionSuppressionRules
{
    public static IEnumerable<Violation> CheckSources(IReadOnlyList<SourceFile> files) =>
        files.SelectMany(Pragmas).Concat(Attributes(files));

    public static IEnumerable<Violation> CheckSwitches(CompilerInvocation invocation) =>
        invocation.NoWarn.Select(id => (Id: id, Switch: "/nowarn"))
            .Concat(invocation.WarningsNotAsErrors.Select(id => (Id: id, Switch: "/warnaserror-")))
            .Where(entry => SonarId().IsMatch(entry.Id.Trim()))
            .Select(entry => new Violation(invocation.ProjectPath, 1, $"{entry.Id.Trim()} is silenced by {entry.Switch} on the compiler command line"));

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

    [GeneratedRegex(@"^S\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex SonarId();
}
