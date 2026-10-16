namespace Core.Tests.Architecture.CodeRules;

/// <summary>Nothing in production looks generated to an analyzer, since analyzers skip generated code.</summary>
public sealed class GeneratedCodeTests
{
    private const string Fixtures = "generated-code";

    [Fact]
    public void ProductionSourcesAreNotMarkedAsGeneratedCode()
    {
        var violations = GeneratedCodeRules.CheckSources(SourceTree.ProductionFiles());

        Assert.Empty(violations);
    }

    [Fact]
    public void PlainSourceIsNotMarkedAsGeneratedCode()
    {
        var violations = GeneratedCodeRules.CheckSources([Fixture("Plain.cs")]);

        Assert.Empty(violations);
    }

    [Theory]
    [InlineData("HeaderDashed.cs")]
    [InlineData("HeaderUndashed.cs")]
    [InlineData("NamedG.g.cs")]
    [InlineData("NamedGI.g.i.cs")]
    [InlineData("NamedDesigner.designer.cs")]
    [InlineData("NamedGenerated.generated.cs")]
    [InlineData("NamedUpper.G.CS")]
    [InlineData("AttributeGeneratedCode.cs")]
    [InlineData("AttributeCompilerGenerated.cs")]
    public void SourceMarkedAsGeneratedCodeIsRefused(string fixtureFile)
    {
        var violations = GeneratedCodeRules.CheckSources([Fixture(fixtureFile)]);

        Assert.NotEmpty(violations);
    }

    private static SourceFile Fixture(string name) => SourceTree.Fixture(Path.Combine(Fixtures, name));
}
