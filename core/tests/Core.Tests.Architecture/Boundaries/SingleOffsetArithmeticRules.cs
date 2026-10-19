using Core.Tests.Architecture.CodeRules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Core.Tests.Architecture.Boundaries;

/// <summary>Code points are counted, and the service's text cut at its offsets, only in the converter's folder; each name is judged by what it binds to, never by how it is spelled.</summary>
internal static class SingleOffsetArithmeticRules
{
    /// <summary>The folder, below <c>core/</c>, where the conversion lives.</summary>
    public const string ConverterFolder = "src/Core.Boundary/Text/";

    private const string EnumerateRunes = "EnumerateRunes";

    private static readonly string[] CountingTypes =
        ["System.Text.Rune", "System.Globalization.StringInfo", "System.Globalization.TextElementEnumerator"];

    private static readonly string[] CountingCharMembers =
        ["IsSurrogate", "IsHighSurrogate", "IsLowSurrogate", "IsSurrogatePair", "ConvertToUtf32", "ConvertFromUtf32"];

    private static readonly string[] CuttingMembers =
        ["Substring", "Remove", "Insert", "AsSpan", "AsMemory", "Slice", "Skip", "Take", "ElementAt", "ToCharArray", "CopyTo"];

    private static readonly string[] TextTypes =
    [
        "string", "System.MemoryExtensions", "System.Linq.Enumerable",
        "System.Span<T>", "System.ReadOnlySpan<T>", "System.Memory<T>", "System.ReadOnlyMemory<T>",
    ];

    /// <summary>The members that hold the text sent to the service and the offsets it sends back.</summary>
    private static readonly string[] ServiceMembers =
        ["Core.Boundary.Contracts.AnalysisRequest.Text", "Core.Boundary.Contracts.Occurrence.Start", "Core.Boundary.Contracts.Occurrence.End"];

    /// <summary>Every finding outside the converter's folder.</summary>
    public static IEnumerable<Violation> Check(IReadOnlyList<SourceFile> files) =>
        Find(files).Where(violation => !InConverterFolder(violation.File));

    /// <summary>Every finding, wherever it is; the files are bound together, so a name in one resolves to a type in another.</summary>
    public static IEnumerable<Violation> Find(IReadOnlyList<SourceFile> files)
    {
        var compilation = SourceCompilation.Of(files);
        return files.SelectMany(file => FindIn(file, compilation.GetSemanticModel(file.Tree))).Distinct();
    }

    public static bool InConverterFolder(string path) =>
        Path.GetRelativePath(SourceTree.CoreRoot, path).Replace('\\', '/').StartsWith(ConverterFolder, StringComparison.Ordinal);

    private static IEnumerable<Violation> FindIn(SourceFile file, SemanticModel model)
    {
        var counting = file.Root.DescendantNodes()
            .OfType<SimpleNameSyntax>()
            .Select(name => (Name: name, Symbol: Bound(model, name)))
            .Where(entry => CountsCodePoints(entry.Symbol))
            .Select(entry => new Violation(file.Path, SourceFile.LineOf(entry.Name), $"{entry.Symbol!.ToDisplayString()} counts code points outside {ConverterFolder}"));
        var cuts = file.Root.DescendantNodes()
            .Where(node => IsCut(model, node) && TouchesTheService(model, node))
            .Select(node => new Violation(file.Path, SourceFile.LineOf(node), $"the service's text or offsets are cut outside {ConverterFolder}"));
        return counting.Concat(cuts);
    }

    private static ISymbol? Bound(SemanticModel model, SyntaxNode node)
    {
        var info = model.GetSymbolInfo(node);
        return info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
    }

    private static bool CountsCodePoints(ISymbol? symbol) => symbol switch
    {
        null => false,
        ITypeSymbol type => IsOneOf(type, CountingTypes),
        IMethodSymbol { Name: EnumerateRunes } => true,
        _ => IsOneOf(symbol.ContainingType, CountingTypes) || IsCharMember(symbol),
    };

    private static bool IsCharMember(ISymbol symbol) =>
        symbol.ContainingType?.SpecialType == SpecialType.System_Char && CountingCharMembers.Contains(symbol.Name, StringComparer.Ordinal);

    /// <summary>A call that takes part of a text, or an index or a range applied to one; a sequence of anything but characters is not a text.</summary>
    private static bool IsCut(SemanticModel model, SyntaxNode node) => node switch
    {
        InvocationExpressionSyntax => Bound(model, node) is IMethodSymbol method
            && CuttingMembers.Contains(method.Name, StringComparer.Ordinal)
            && IsOneOf(method.ContainingType, TextTypes)
            && OnlyCharacters(method.TypeArguments.Concat(method.ContainingType.TypeArguments)),
        ElementAccessExpressionSyntax access => model.GetTypeInfo(access.Expression).Type is INamedTypeSymbol type
            && IsOneOf(type, TextTypes)
            && OnlyCharacters(type.TypeArguments),
        _ => false,
    };

    /// <summary>A string has no type argument; <c>Skip&lt;char&gt;</c> or <c>ReadOnlySpan&lt;char&gt;</c> holds characters; <c>Skip&lt;Occurrence&gt;</c> does not.</summary>
    private static bool OnlyCharacters(IEnumerable<ITypeSymbol> typeArguments) =>
        typeArguments.All(argument => argument.SpecialType == SpecialType.System_Char);

    private static bool TouchesTheService(SemanticModel model, SyntaxNode node) =>
        node.DescendantNodes()
            .OfType<SimpleNameSyntax>()
            .Select(name => Bound(model, name))
            .OfType<IPropertySymbol>()
            .Any(property => ServiceMembers.Contains($"{property.ContainingType.ToDisplayString()}.{property.Name}", StringComparer.Ordinal));

    private static bool IsOneOf(ITypeSymbol? type, string[] names) =>
        type is not null && names.Contains(type.OriginalDefinition.ToDisplayString(), StringComparer.Ordinal);
}
