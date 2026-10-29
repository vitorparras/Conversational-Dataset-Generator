using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>
/// What starts the real language service, or needs the interpreter uv resolves for it: the entry points of the host tests, and
/// every helper that reaches one of them under a name no other member of the tests shares.
/// </summary>
internal static class RealServiceUse
{
    private const string RealHostStart = "StartWithTheRealServiceAsync";
    private const string RealInstallation = "UsesRealService";
    private const string RealArgument = "realService";
    private const string Interpreter = "LanguageServiceInterpreter";
    private static readonly string[] InterpreterMembers = ["Path", "Available", "Standalone"];

    /// <summary>The names of the helpers that reach an entry point, directly or through another helper; a name two members share is left out.</summary>
    public static IReadOnlySet<string> HelperNames(IEnumerable<SourceFile> files)
    {
        var candidates = files
            .SelectMany(file => file.Root.DescendantNodes())
            .Where(node => node is MethodDeclarationSyntax or PropertyDeclarationSyntax or VariableDeclaratorSyntax { Parent.Parent: FieldDeclarationSyntax })
            .Where(node => node is not MethodDeclarationSyntax method || !IsTest(method))
            .GroupBy(NameOf, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        var helpers = new HashSet<string>(StringComparer.Ordinal);
        var reached = candidates.Where(candidate => Reaches(candidate.Value, helpers)).Select(candidate => candidate.Key).ToList();
        while (reached.Count > 0)
        {
            helpers.UnionWith(reached);
            reached = [.. candidates.Where(candidate => !helpers.Contains(candidate.Key) && Reaches(candidate.Value, helpers)).Select(candidate => candidate.Key)];
        }

        return helpers;
    }
    public static bool Reaches(SyntaxNode node, IReadOnlySet<string> helpers) =>
        node.DescendantNodesAndSelf().Any(inner => IsEntryPoint(inner) || IsHelperCall(inner, helpers));

    public static bool IsTest(MemberDeclarationSyntax member) =>
        member.AttributeLists.SelectMany(list => list.Attributes)
            .Select(attribute => ServiceTypeIndex.SimpleName(attribute.Name))
            .Any(name => name.EndsWith("Fact", StringComparison.Ordinal) || name.EndsWith("Theory", StringComparison.Ordinal));

    private static string NameOf(SyntaxNode node) => node switch
    {
        MethodDeclarationSyntax method => method.Identifier.ValueText,
        PropertyDeclarationSyntax property => property.Identifier.ValueText,
        VariableDeclaratorSyntax field => field.Identifier.ValueText,
        _ => string.Empty,
    };

    private static bool IsHelperCall(SyntaxNode node, IReadOnlySet<string> helpers) =>
        node is IdentifierNameSyntax name && helpers.Contains(name.Identifier.ValueText);

    private static bool IsEntryPoint(SyntaxNode node) => node switch
    {
        IdentifierNameSyntax name => name.Identifier.ValueText == RealHostStart,
        AssignmentExpressionSyntax { Left: IdentifierNameSyntax left } assignment =>
            left.Identifier.ValueText == RealInstallation && assignment.Right.IsKind(SyntaxKind.TrueLiteralExpression),
        ArgumentSyntax { NameColon: { } label } argument =>
            label.Name.Identifier.ValueText == RealArgument && !argument.Expression.IsKind(SyntaxKind.FalseLiteralExpression),
        MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax owner } access =>
            owner.Identifier.ValueText == Interpreter && InterpreterMembers.Contains(access.Name.Identifier.ValueText),
        _ => false,
    };
}
