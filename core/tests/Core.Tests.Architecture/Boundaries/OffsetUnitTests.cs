using Core.Tests.Architecture.CodeRules;

namespace Core.Tests.Architecture.Boundaries;

/// <summary>The unit the core stores a limit in is declared once, and every place that stores a limit writes that unit.</summary>
public sealed class OffsetUnitTests
{
    [Fact]
    public void OffsetUnitIsDeclaredOnceAndUsedToStore()
    {
        var files = SourceTree.ProductionFiles();

        var declaring = OffsetUnitRules.Declaring(files);
        var storing = OffsetUnitRules.StoringWithTheDeclaredUnit(files);

        Assert.Equal(["src/Core.Platform/Text/OffsetUnit.cs"], declaring);
        Assert.Equal(OffsetUnitRules.Storing.Order(), storing);
    }

    [Fact]
    public void AStoreThatWritesTheUnitByItselfIsNotUsingTheDeclaration()
    {
        var own = SourceFile.FromText(
            Path.Combine(SourceTree.CoreRoot, "src", "Core.Ingestion", "Sanitizing", "TrailJson.cs"),
            "namespace Fixtures;\n\ninternal static class TrailJson\n{\n    internal const string Unit = \"code-point\";\n}\n");

        var declaring = OffsetUnitRules.Declaring([own]);
        var storing = OffsetUnitRules.StoringWithTheDeclaredUnit([own]);

        Assert.Equal(["src/Core.Ingestion/Sanitizing/TrailJson.cs"], declaring);
        Assert.Empty(storing);
    }
}
