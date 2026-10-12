using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>Code rule 2 at file level: length, one public type, named types in public signatures.</summary>
internal static class FileLimits
{
    public const int MaxLines = 250;

    public static IReadOnlyList<Violation> Check(SourceFile file) =>
        [.. CheckLength(file), .. CheckPublicTypes(file), .. CheckTuplesInPublicSignatures(file)];

    public static IEnumerable<Violation> CheckLength(SourceFile file)
    {
        if (file.LineCount > MaxLines)
        {
            yield return new Violation(file.Path, 1, $"file has {file.LineCount} lines; the limit is {MaxLines}");
        }
    }

    public static IEnumerable<Violation> CheckPublicTypes(SourceFile file)
    {
        var publicTypes = file.Root.DescendantNodes()
            .OfType<MemberDeclarationSyntax>()
            .Where(IsTopLevelType)
            .Where(type => type.Modifiers.Any(SyntaxKind.PublicKeyword))
            .ToList();
        return publicTypes.Skip(1).Select(type => new Violation(
            file.Path, SourceFile.LineOf(type), $"a second public type in the file ({publicTypes.Count} in all)"));
    }

    public static IEnumerable<Violation> CheckTuplesInPublicSignatures(SourceFile file) =>
        file.Root.DescendantNodes()
            .OfType<MemberDeclarationSyntax>()
            .Where(IsPublicSignature)
            .Where(member => SignatureTypes(member).Any(ContainsTuple))
            .Select(member => new Violation(
                file.Path, SourceFile.LineOf(member), "tuple in a public signature; declare a named type"));

    private static bool IsTopLevelType(MemberDeclarationSyntax member) =>
        member is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax
        && member.Parent is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax;

    private static bool IsPublicSignature(MemberDeclarationSyntax member) =>
        member.Modifiers.Any(SyntaxKind.PublicKeyword)
        && member is BaseMethodDeclarationSyntax or BasePropertyDeclarationSyntax
            or DelegateDeclarationSyntax or TypeDeclarationSyntax;

    private static bool ContainsTuple(TypeSyntax type) =>
        type.DescendantNodesAndSelf().OfType<TupleTypeSyntax>().Any();

    private static IEnumerable<TypeSyntax> SignatureTypes(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax method => [method.ReturnType, .. ParameterTypes(method.ParameterList)],
        ConstructorDeclarationSyntax constructor => ParameterTypes(constructor.ParameterList),
        BasePropertyDeclarationSyntax property => [property.Type],
        DelegateDeclarationSyntax @delegate => [@delegate.ReturnType, .. ParameterTypes(@delegate.ParameterList)],
        TypeDeclarationSyntax type => ParameterTypes(type.ParameterList),
        _ => [],
    };

    private static IEnumerable<TypeSyntax> ParameterTypes(ParameterListSyntax? parameters) =>
        parameters?.Parameters.Select(parameter => parameter.Type).OfType<TypeSyntax>() ?? [];
}
