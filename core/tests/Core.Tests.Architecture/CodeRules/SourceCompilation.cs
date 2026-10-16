using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>Binds source files against the runtime's own assemblies so that rules can ask what a name means, not how it is spelled.</summary>
internal static class SourceCompilation
{
    private static readonly Lazy<IReadOnlyList<MetadataReference>> RuntimeReferences = new(ReadRuntimeReferences);

    public static CSharpCompilation Of(IEnumerable<SourceFile> files) =>
        CSharpCompilation.Create(
            "Bound",
            files.Select(file => file.Tree),
            RuntimeReferences.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static IReadOnlyList<MetadataReference> ReadRuntimeReferences() =>
        [.. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))];
}
