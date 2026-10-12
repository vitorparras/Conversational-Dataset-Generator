namespace Core.Tests.Architecture.CodeRules;

/// <summary>Code rule 3 over every production file, with one fixture that breaks each part of it.</summary>
public sealed class CompositionRootTests
{
    [Fact]
    public void EntryPointHasAtMostTwentyLines()
    {
        var violations = SourceTree.ProductionFiles().SelectMany(CompositionRootRules.CheckEntryPointLength);

        Assert.Empty(violations);
    }

    [Fact]
    public void OnlyTheCompositionRootRegistersServices()
    {
        var violations = SourceTree.ProductionFiles().SelectMany(CompositionRootRules.CheckRegistrationOutsideRoot);

        Assert.Empty(violations);
    }

    [Fact]
    public void NoServiceIsConstructedOutsideTheCompositionRoot()
    {
        var files = SourceTree.ProductionFiles();
        var index = ServiceTypeIndex.From(files);

        var violations = files.SelectMany(file => CompositionRootRules.CheckServiceConstructionOutsideRoot(file, index));

        Assert.Empty(violations);
    }

    [Fact]
    public void EntryPointOverTwentyLinesIsRefused()
    {
        var fixture = SourceTree.Fixture(Path.Combine("oversized-program", "Program.cs"));

        var violation = Assert.Single(CompositionRootRules.CheckEntryPointLength(fixture));

        Assert.Contains("21 lines", violation.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RegistrationOutsideTheCompositionRootIsRefused()
    {
        var fixture = SourceTree.Fixture("RegistrationOutsideRoot.cs");

        var violations = CompositionRootRules.CheckRegistrationOutsideRoot(fixture).ToList();

        Assert.Equal([7, 9], violations.Select(violation => violation.Line));
    }

    [Fact]
    public void ServiceConstructedOutsideTheCompositionRootIsRefused()
    {
        var fixture = SourceTree.Fixture("NewServiceOutsideRoot.cs");
        var index = ServiceTypeIndex.From([fixture]);

        var violation = Assert.Single(CompositionRootRules.CheckServiceConstructionOutsideRoot(fixture, index));

        Assert.Contains("Reporter", violation.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TargetTypedServiceConstructionOutsideTheCompositionRootIsRefused()
    {
        var fixture = SourceTree.Fixture("ImplicitNewServiceOutsideRoot.cs");
        var index = ServiceTypeIndex.From([fixture]);

        var violations = CompositionRootRules.CheckServiceConstructionOutsideRoot(fixture, index).ToList();

        Assert.Equal([15, 17, 21], violations.Select(violation => violation.Line));
    }

    [Fact]
    public void TypeWithoutDependenciesIsNotAService()
    {
        var fixture = SourceTree.Fixture("NewServiceOutsideRoot.cs");
        var index = ServiceTypeIndex.From([fixture]);

        Assert.False(index.IsService("Clock"));
    }
}
