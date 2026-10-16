using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>A short entry point, one place that assembles each executable, and no service built anywhere else.</summary>
internal static class CompositionRootRules
{
    public const string EntryPoint = "Program.cs";
    public const int MaxEntryPointLines = 20;
    private const string ServiceCollection = "IServiceCollection";

    public static IEnumerable<Violation> CheckEntryPointLength(SourceFile file)
    {
        if (file.FileName == EntryPoint && file.LineCount > MaxEntryPointLines)
        {
            yield return new Violation(
                file.Path, 1, $"{EntryPoint} has {file.LineCount} lines; the limit is {MaxEntryPointLines}");
        }
    }

    public static IEnumerable<Violation> CheckRegistrationOutsideRoot(SourceFile file, CompositionRoots roots)
    {
        if (roots.IsRoot(file))
        {
            return [];
        }

        var aliases = ServiceCollectionAliases(file);
        var collectionReferences = file.Root.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(name => name.Identifier.ValueText == ServiceCollection)
            .Select(name => new Violation(
                file.Path, SourceFile.LineOf(name), $"{ServiceCollection} is used outside the composition root"));
        var registrations = file.Root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => IsRegistration(invocation, aliases.Contains))
            .Select(invocation => new Violation(
                file.Path, SourceFile.LineOf(invocation), "service registered outside the composition root"));
        return collectionReferences.Concat(registrations);
    }

    public static IEnumerable<Violation> CheckServiceConstructionOutsideRoot(
        SourceFile file, ServiceTypeIndex index, CompositionRoots roots)
    {
        if (roots.IsRoot(file))
        {
            return [];
        }

        var explicitCreations = file.Root.DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .Where(creation => index.IsService(creation.Type))
            .Select(creation => Construction(file, creation, ServiceTypeIndex.SimpleName(creation.Type)));
        var implicitCreations = file.Root.DescendantNodes()
            .OfType<ImplicitObjectCreationExpressionSyntax>()
            .Select(creation => (Creation: creation, Type: DeclaredTypeOf(creation)))
            .Where(entry => entry.Type is not null && index.IsService(entry.Type))
            .Select(entry => Construction(file, entry.Creation, ServiceTypeIndex.SimpleName(entry.Type!)));
        return explicitCreations.Concat(implicitCreations);
    }

    private static Violation Construction(SourceFile file, SyntaxNode creation, string typeName) =>
        new(file.Path, SourceFile.LineOf(creation), $"service {typeName} constructed outside the composition root");

    /// <summary>Locals that hold the collection, as in <c>var collection = builder.Services;</c>.</summary>
    private static HashSet<string> ServiceCollectionAliases(SourceFile file) =>
        file.Root.DescendantNodes()
            .OfType<VariableDeclaratorSyntax>()
            .Where(declarator => declarator.Initializer is { Value: var value } && LooksLikeServiceCollection(value.ToString()))
            .Select(declarator => declarator.Identifier.ValueText)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>A call such as <c>services.AddSingleton&lt;T&gt;()</c> or <c>builder.Services.TryAddScoped(...)</c>.</summary>
    private static bool IsRegistration(InvocationExpressionSyntax invocation, Func<string, bool> isAlias) =>
        invocation.Expression is MemberAccessExpressionSyntax access
        && IsRegistrationName(access.Name.Identifier.ValueText)
        && (LooksLikeServiceCollection(access.Expression.ToString()) || isAlias(access.Expression.ToString()));

    private static bool IsRegistrationName(string name) =>
        name.StartsWith("Add", StringComparison.Ordinal) || name.StartsWith("TryAdd", StringComparison.Ordinal);

    private static bool LooksLikeServiceCollection(string expression) =>
        expression.EndsWith("ervices", StringComparison.Ordinal);

    /// <summary>The type a target-typed <c>new(...)</c> resolves to when it initialises a variable, field or property.</summary>
    private static TypeSyntax? DeclaredTypeOf(SyntaxNode creation) =>
        creation.Parent?.Parent switch
        {
            VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration } => declaration.Type,
            PropertyDeclarationSyntax property => property.Type,
            _ => null,
        };
}
