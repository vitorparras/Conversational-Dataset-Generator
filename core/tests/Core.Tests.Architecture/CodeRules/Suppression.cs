using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>One attribute that the compiler honours as a suppression, found by what it binds to and not by how it is written.</summary>
internal sealed class Suppression
{
    private static readonly string[] Types =
    [
        "System.Diagnostics.CodeAnalysis.SuppressMessageAttribute",
        "System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessageAttribute",
    ];

    private readonly SemanticModel model;

    private Suppression(SourceFile file, AttributeSyntax attribute, SemanticModel model)
    {
        File = file;
        Attribute = attribute;
        this.model = model;
    }

    public SourceFile File { get; }

    public AttributeSyntax Attribute { get; }

    public int Line => SourceFile.LineOf(Attribute);

    /// <summary>The check id the compiler reads, whether it is written as a literal or hidden behind a constant; null when it has none.</summary>
    public string? CheckId => IdArgument() is { } argument ? model.GetConstantValue(argument.Expression).Value as string : null;

    public bool IdIsLiteral => IdArgument()?.Expression is LiteralExpressionSyntax;

    public INamedTypeSymbol? AttributedClass =>
        Attribute.Parent is AttributeListSyntax { Target: null, Parent: ClassDeclarationSyntax type }
            ? model.GetDeclaredSymbol(type)
            : null;

    public static IEnumerable<Suppression> FindIn(SourceFile file, SemanticModel model) =>
        file.Root.DescendantNodes()
            .OfType<AttributeSyntax>()
            .Where(attribute => IsSuppressionType(model, attribute))
            .Select(attribute => new Suppression(file, attribute, model));

    private static bool IsSuppressionType(SemanticModel model, AttributeSyntax attribute)
    {
        var info = model.GetSymbolInfo(attribute);
        var type = (info.Symbol ?? info.CandidateSymbols.FirstOrDefault())?.ContainingType;
        return type is null
            ? SpelledAsSuppression(attribute)
            : Chain(type).Any(link => Types.Contains(link.ToDisplayString()));
    }

    private static bool SpelledAsSuppression(AttributeSyntax attribute) =>
        ServiceTypeIndex.SimpleName(attribute.Name) is "SuppressMessage" or "SuppressMessageAttribute"
            or "UnconditionalSuppressMessage" or "UnconditionalSuppressMessageAttribute";

    private static IEnumerable<INamedTypeSymbol> Chain(INamedTypeSymbol type)
    {
        for (var link = type; link is not null; link = link.BaseType)
        {
            yield return link;
        }
    }

    private AttributeArgumentSyntax? IdArgument() =>
        Attribute.ArgumentList?.Arguments
            .Where(argument => argument.NameEquals is null && argument.NameColon is null)
            .Skip(1)
            .FirstOrDefault();
}
