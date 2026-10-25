using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>Limits a method: its length, parameter count and nesting depth.</summary>
internal static class MethodLimits
{
    private static readonly HashSet<SyntaxKind> NestingKinds =
    [
        SyntaxKind.IfStatement, SyntaxKind.ForStatement, SyntaxKind.ForEachStatement, SyntaxKind.WhileStatement,
        SyntaxKind.DoStatement, SyntaxKind.SwitchStatement, SyntaxKind.TryStatement, SyntaxKind.UsingStatement,
        SyntaxKind.LockStatement,
    ];

    public const int MaxLines = 40;
    public const int MaxParameters = 4;
    public const int MaxNesting = 2;

    public static IReadOnlyList<Violation> Check(SourceFile file) =>
        [.. CheckLength(file), .. CheckParameters(file), .. CheckNesting(file)];

    public static IEnumerable<Violation> CheckLength(SourceFile file) =>
        Methods(file)
            .Where(method => SourceFile.LinesSpannedBy(method) > MaxLines)
            .Select(method => new Violation(
                file.Path,
                SourceFile.LineOf(method),
                $"method spans {SourceFile.LinesSpannedBy(method)} lines; the limit is {MaxLines}"));

    /// <summary>
    /// An explicit interface implementation is left out: its signature is the interface's, counted where the interface
    /// is declared when it is ours, and imposed from outside the repository when it is not.
    /// </summary>
    public static IEnumerable<Violation> CheckParameters(SourceFile file) =>
        Methods(file)
            .Where(method => method is not MethodDeclarationSyntax { ExplicitInterfaceSpecifier: not null })
            .Where(method => ParameterList(method).Parameters.Count > MaxParameters)
            .Select(method => new Violation(
                file.Path,
                SourceFile.LineOf(method),
                $"method takes {ParameterList(method).Parameters.Count} parameters; the limit is {MaxParameters}"));

    public static IEnumerable<Violation> CheckNesting(SourceFile file) =>
        Methods(file)
            .SelectMany(method => method.DescendantNodes()
                .Where(IsNestingStatement)
                .Where(statement => DepthWithin(method, statement) > MaxNesting)
                .Take(1))
            .Select(statement => new Violation(
                file.Path, SourceFile.LineOf(statement), $"statement nested deeper than {MaxNesting} levels"));

    /// <summary>Methods, constructors and local functions; a record parameter list is data, not a signature.</summary>
    private static IEnumerable<SyntaxNode> Methods(SourceFile file) =>
        file.Root.DescendantNodes()
            .Where(node => node is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax);

    private static ParameterListSyntax ParameterList(SyntaxNode method) => method switch
    {
        BaseMethodDeclarationSyntax declaration => declaration.ParameterList,
        LocalFunctionStatementSyntax local => local.ParameterList,
        _ => throw new ArgumentException("Not a method node.", nameof(method)),
    };

    private static bool IsNestingStatement(SyntaxNode node) => NestingKinds.Contains(node.Kind());

    /// <summary>Each nesting statement between the method and the node adds a level; an else-if does not.</summary>
    private static int DepthWithin(SyntaxNode method, SyntaxNode statement) =>
        statement.AncestorsAndSelf()
            .TakeWhile(ancestor => ancestor != method)
            .Count(ancestor => IsNestingStatement(ancestor) && ancestor.Parent is not ElseClauseSyntax);
}
