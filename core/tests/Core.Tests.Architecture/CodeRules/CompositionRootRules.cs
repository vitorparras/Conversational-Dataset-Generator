using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>Code rule 3: a short entry point, one composition root, no service built elsewhere.</summary>
internal static class CompositionRootRules
{
    public const string EntryPoint = "Program.cs";
    public const string Root = "CompositionRoot.cs";
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

    public static IEnumerable<Violation> CheckRegistrationOutsideRoot(SourceFile file)
    {
        if (file.FileName == Root)
        {
            return [];
        }

        var collectionReferences = file.Root.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(name => name.Identifier.ValueText == ServiceCollection)
            .Select(name => new Violation(
                file.Path, SourceFile.LineOf(name), $"{ServiceCollection} is used outside {Root}"));
        var registrations = file.Root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(IsRegistration)
            .Select(invocation => new Violation(
                file.Path, SourceFile.LineOf(invocation), $"service registered outside {Root}"));
        return collectionReferences.Concat(registrations);
    }

    public static IEnumerable<Violation> CheckServiceConstructionOutsideRoot(SourceFile file, ServiceTypeIndex index)
    {
        if (file.FileName == Root)
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
        new(file.Path, SourceFile.LineOf(creation), $"service {typeName} constructed outside {Root}");

    /// <summary>A call such as <c>services.AddSingleton&lt;T&gt;()</c> or <c>builder.Services.TryAddScoped(...)</c>.</summary>
    private static bool IsRegistration(InvocationExpressionSyntax invocation) =>
        invocation.Expression is MemberAccessExpressionSyntax access
        && IsRegistrationName(access.Name.Identifier.ValueText)
        && access.Expression.ToString().EndsWith("ervices", StringComparison.Ordinal);

    private static bool IsRegistrationName(string name) =>
        name.StartsWith("Add", StringComparison.Ordinal) || name.StartsWith("TryAdd", StringComparison.Ordinal);

    /// <summary>The type a target-typed <c>new(...)</c> resolves to when it initialises a variable, field or property.</summary>
    private static TypeSyntax? DeclaredTypeOf(ImplicitObjectCreationExpressionSyntax creation) =>
        creation.Parent?.Parent switch
        {
            VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration } => declaration.Type,
            PropertyDeclarationSyntax property => property.Type,
            _ => null,
        };
}
