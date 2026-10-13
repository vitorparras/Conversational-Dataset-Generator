using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>Which declared types are services: a class or struct whose constructor receives another declared type.</summary>
internal sealed class ServiceTypeIndex
{
    private readonly HashSet<string> services;

    private ServiceTypeIndex(HashSet<string> services)
    {
        this.services = services;
    }

    public static ServiceTypeIndex From(IEnumerable<SourceFile> files)
    {
        var declared = files
            .SelectMany(file => file.Root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            .ToList();
        var declaredNames = declared.Select(type => type.Identifier.ValueText).ToHashSet(StringComparer.Ordinal);
        var services = declared
            .Where(type => type is ClassDeclarationSyntax or StructDeclarationSyntax)
            .Where(type => ConstructorParameterTypes(type).Any(declaredNames.Contains))
            .Select(type => type.Identifier.ValueText)
            .ToHashSet(StringComparer.Ordinal);
        return new ServiceTypeIndex(services);
    }

    public bool IsService(TypeSyntax type) => services.Contains(SimpleName(type));

    public bool IsService(string typeName) => services.Contains(typeName);

    /// <summary>The identifier of a type reference, without namespace, generic arguments or nullability.</summary>
    public static string SimpleName(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        GenericNameSyntax generic => generic.Identifier.ValueText,
        QualifiedNameSyntax qualified => SimpleName(qualified.Right),
        NullableTypeSyntax nullable => SimpleName(nullable.ElementType),
        _ => type.ToString(),
    };

    private static IEnumerable<string> ConstructorParameterTypes(TypeDeclarationSyntax type)
    {
        var explicitParameters = type.Members
            .OfType<ConstructorDeclarationSyntax>()
            .SelectMany(constructor => constructor.ParameterList.Parameters);
        var primaryParameters = type.ParameterList?.Parameters ?? [];
        return explicitParameters.Concat(primaryParameters)
            .Select(parameter => parameter.Type)
            .OfType<TypeSyntax>()
            .Select(SimpleName);
    }
}
