using Core.Tests.Architecture.CodeRules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Core.Tests.Architecture.Boundaries;

/// <summary>The unit a limit of a span counts in is written in one file of the core, and what stores a limit names that declaration, never a unit of its own.</summary>
internal static class OffsetUnitRules
{
    /// <summary>The name of the unit, as a stored limit declares it.</summary>
    public const string UnitName = "code-point";

    /// <summary>The files, below <c>core/</c>, that store the limits of a span: the trail of the sanitization and the occurrences of an item.</summary>
    public static readonly string[] Storing = ["src/Core.Ingestion/Sanitizing/TrailJson.cs", "src/Core.Persistence/Content/OccurrenceJson.cs"];

    private const string Declaration = "Core.Platform.Text.OffsetUnit.CodePoint";

    /// <summary>Every file that writes the name of the unit, which is where the unit is declared.</summary>
    public static IReadOnlyList<string> Declaring(IEnumerable<SourceFile> files) =>
        [.. files.Where(file => file.Root.DescendantTokens().Any(token => token.IsKind(SyntaxKind.StringLiteralToken) && token.ValueText == UnitName)).Select(file => Relative(file.Path)).Order()];

    /// <summary>The files that store limits and name the one declaration of the unit, judged by what each name binds to.</summary>
    public static IReadOnlyList<string> StoringWithTheDeclaredUnit(IReadOnlyList<SourceFile> files)
    {
        var compilation = SourceCompilation.Of(files);
        return [.. files
            .Where(file => Storing.Contains(Relative(file.Path), StringComparer.Ordinal) && NamesTheDeclaration(file, compilation.GetSemanticModel(file.Tree)))
            .Select(file => Relative(file.Path))
            .Order()];
    }

    private static bool NamesTheDeclaration(SourceFile file, SemanticModel model) =>
        file.Root.DescendantNodes()
            .OfType<SimpleNameSyntax>()
            .Select(name => model.GetSymbolInfo(name).Symbol)
            .OfType<IPropertySymbol>()
            .Any(property => $"{property.ContainingType.ToDisplayString()}.{property.Name}" == Declaration);

    private static string Relative(string path) => Path.GetRelativePath(SourceTree.CoreRoot, path).Replace('\\', '/');
}
