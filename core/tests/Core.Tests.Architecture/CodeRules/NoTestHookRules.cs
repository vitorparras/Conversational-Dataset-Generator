using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>Internals visible only to the module's own tests, and no public member named for a test.</summary>
internal static class NoTestHookRules
{
    private const string Attribute = "InternalsVisibleTo";
    private const string TestMarker = "Test";

    public static IEnumerable<Violation> CheckInternalsVisibleTo(ProjectFile project) =>
        project.InternalsVisibleTo()
            .Where(grant => !IsOwnTestProject(project.ModuleName, grant.Target))
            .Select(grant => new Violation(project.Path, grant.Line, ForeignInternals(project.ModuleName, grant.Target)));

    public static IEnumerable<Violation> CheckInternalsVisibleTo(SourceFile file, string moduleName) =>
        file.Root.DescendantNodes()
            .OfType<AttributeSyntax>()
            .Where(IsInternalsVisibleTo)
            .Select(attribute => (Attribute: attribute, Target: FirstStringArgument(attribute)))
            .Where(entry => entry.Target is not null && !IsOwnTestProject(moduleName, entry.Target))
            .Select(entry => new Violation(file.Path, SourceFile.LineOf(entry.Attribute), ForeignInternals(moduleName, entry.Target!)));

    public static IEnumerable<Violation> CheckPublicMemberNames(SourceFile file) =>
        file.Root.DescendantNodes()
            .OfType<MemberDeclarationSyntax>()
            .Where(member => member.Modifiers.Any(SyntaxKind.PublicKeyword))
            .SelectMany(member => Identifiers(member).Select(identifier => (Member: member, Identifier: identifier)))
            .Where(entry => entry.Identifier.ValueText.Contains(TestMarker, StringComparison.Ordinal))
            .Select(entry => new Violation(
                file.Path, SourceFile.LineOf(entry.Member), $"public member {entry.Identifier.ValueText} is named for a test"));

    /// <summary>
    /// Only <c>&lt;Module&gt;.Tests</c>, with an optional <c>.Suffix</c>, or the test project of one area of the module,
    /// <c>&lt;Module&gt;.&lt;Area&gt;.Tests</c>, may see the module's internals.
    /// </summary>
    private static bool IsOwnTestProject(string moduleName, string target) =>
        target == moduleName + ".Tests"
        || target.StartsWith(moduleName + ".Tests.", StringComparison.Ordinal)
        || (target.StartsWith(moduleName + ".", StringComparison.Ordinal) && target.EndsWith(".Tests", StringComparison.Ordinal));

    private static bool IsInternalsVisibleTo(AttributeSyntax attribute) =>
        ServiceTypeIndex.SimpleName(attribute.Name) is Attribute or Attribute + "Attribute";

    private static string ForeignInternals(string moduleName, string target) =>
        $"internals of {moduleName} visible to {target}; only {moduleName}.Tests or {moduleName}.Tests.* may see them";

    private static string? FirstStringArgument(AttributeSyntax attribute) =>
        attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax literal
            ? literal.Token.ValueText
            : null;

    private static IEnumerable<SyntaxToken> Identifiers(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax method => [method.Identifier],
        PropertyDeclarationSyntax property => [property.Identifier],
        EventDeclarationSyntax @event => [@event.Identifier],
        BaseTypeDeclarationSyntax type => [type.Identifier],
        DelegateDeclarationSyntax @delegate => [@delegate.Identifier],
        BaseFieldDeclarationSyntax field => field.Declaration.Variables.Select(variable => variable.Identifier),
        _ => [],
    };
}
