using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>
/// A test that starts the real language service carries <c>[Trait("Category", "RealService")]</c>, on the method or on its class,
/// so the core suite can leave it out and a step of its own can run it. <c>LogDestinationsTests</c> is the declared exception: it
/// never starts the service, because it proves that a run without the service fails and has to stay in the core suite (issue #35, CA-3).
/// </summary>
internal static class RealServiceMarkRules
{
    private const string Category = "Category";
    private const string Mark = "RealService";

    /// <summary>The marks missing from the tests of the files; a type in <paramref name="pending"/> is left to the change that marks it.</summary>
    public static IEnumerable<Violation> Check(IReadOnlyList<SourceFile> files, IReadOnlySet<string> pending)
    {
        var helpers = RealServiceUse.HelperNames(files);
        return files.SelectMany(file => file.Root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(RealServiceUse.IsTest)
            .Where(method => RealServiceUse.Reaches(method, helpers))
            .Where(method => !IsMarked(method) && !pending.Contains(TypeOf(method)))
            .Select(method => new Violation(
                file.Path, SourceFile.LineOf(method), $"{TypeOf(method)}.{method.Identifier.ValueText} starts the real service without [Trait(\"{Category}\", \"{Mark}\")]")));
    }

    private static bool IsMarked(SyntaxNode method) =>
        method.AncestorsAndSelf().OfType<MemberDeclarationSyntax>().Any(HasMark);

    private static bool HasMark(MemberDeclarationSyntax member) =>
        member.AttributeLists.SelectMany(list => list.Attributes).Any(IsMark);

    private static bool IsMark(AttributeSyntax attribute) =>
        ServiceTypeIndex.SimpleName(attribute.Name) is "Trait" or "TraitAttribute"
        && attribute.ArgumentList?.Arguments.Select(argument => (argument.Expression as LiteralExpressionSyntax)?.Token.ValueText)
            .SequenceEqual([Category, Mark]) == true;

    private static string TypeOf(SyntaxNode method) =>
        method.Ancestors().OfType<TypeDeclarationSyntax>().First().Identifier.ValueText;
}
