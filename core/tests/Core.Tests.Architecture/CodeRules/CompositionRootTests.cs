namespace Core.Tests.Architecture.CodeRules;

/// <summary>The entry point stays short and only the composition root wires services, with fixtures that break each part.</summary>
public sealed class CompositionRootTests
{
    private static readonly CompositionRoots NoRoots = CompositionRoots.From([]);

    [Fact]
    public void EntryPointHasAtMostTwentyLines()
    {
        var violations = SourceTree.ProductionFiles().SelectMany(CompositionRootRules.CheckEntryPointLength);

        Assert.Empty(violations);
    }

    [Fact]
    public void EveryExecutableHasExactlyOneCompositionRoot()
    {
        var roots = CompositionRoots.From(SourceTree.ProductionProjects());

        var violations = roots.CheckOnePerExecutable(SourceTree.ProductionFiles());

        Assert.Empty(violations);
    }

    [Fact]
    public void OnlyTheCompositionRootRegistersServices()
    {
        var roots = CompositionRoots.From(SourceTree.ProductionProjects());

        var violations = SourceTree.ProductionFiles()
            .SelectMany(file => CompositionRootRules.CheckRegistrationOutsideRoot(file, roots));

        Assert.Empty(violations);
    }

    [Fact]
    public void NoServiceIsConstructedOutsideTheCompositionRoot()
    {
        var files = SourceTree.ProductionFiles();
        var index = ServiceTypeIndex.From(files);
        var roots = CompositionRoots.From(SourceTree.ProductionProjects());

        var violations = files.SelectMany(file => CompositionRootRules.CheckServiceConstructionOutsideRoot(file, index, roots));

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
    public void SecondCompositionRootInAnExecutableIsRefused()
    {
        var roots = CompositionRoots.From([SourceTree.FixtureProject("second-root/Core.Sample/Core.Sample.csproj.txt")]);

        var violation = Assert.Single(roots.CheckOnePerExecutable(SourceTree.FixtureFiles("second-root")));

        Assert.EndsWith(Path.Combine("Startup", "CompositionRoot.cs"), violation.File, StringComparison.Ordinal);
    }

    [Fact]
    public void CompositionRootInALibraryIsRefusedAsMisplaced()
    {
        var roots = CompositionRoots.From([SourceTree.FixtureProject("library-root/Core.Library/Core.Library.csproj.txt")]);

        var violation = Assert.Single(roots.CheckOnePerExecutable(SourceTree.FixtureFiles("library-root")));

        Assert.EndsWith("CompositionRoot.cs", violation.File, StringComparison.Ordinal);
    }

    [Fact]
    public void CompositionRootInALibraryIsNotExemptFromTheRegistrationRule()
    {
        var roots = CompositionRoots.From([SourceTree.FixtureProject("library-root/Core.Library/Core.Library.csproj.txt")]);

        var violations = SourceTree.FixtureFiles("library-root")
            .SelectMany(file => CompositionRootRules.CheckRegistrationOutsideRoot(file, roots))
            .ToList();

        Assert.Equal([7, 9], violations.Select(violation => violation.Line));
    }

    [Fact]
    public void RegistrationOutsideTheCompositionRootIsRefused()
    {
        var fixture = SourceTree.Fixture("RegistrationOutsideRoot.cs");

        var violations = CompositionRootRules.CheckRegistrationOutsideRoot(fixture, NoRoots).ToList();

        Assert.Equal([7, 9], violations.Select(violation => violation.Line));
    }

    [Fact]
    public void RegistrationThroughAnAliasOfTheCollectionIsRefused()
    {
        var fixture = SourceTree.Fixture("RegistrationViaAlias.cs");

        var violation = Assert.Single(CompositionRootRules.CheckRegistrationOutsideRoot(fixture, NoRoots));

        Assert.Equal(10, violation.Line);
    }

    [Fact]
    public void ServiceConstructedOutsideTheCompositionRootIsRefused()
    {
        var fixture = SourceTree.Fixture("NewServiceOutsideRoot.cs");
        var index = ServiceTypeIndex.From([fixture]);

        var violation = Assert.Single(CompositionRootRules.CheckServiceConstructionOutsideRoot(fixture, index, NoRoots));

        Assert.Contains("Reporter", violation.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TargetTypedServiceConstructionOutsideTheCompositionRootIsRefused()
    {
        var fixture = SourceTree.Fixture("ImplicitNewServiceOutsideRoot.cs");
        var index = ServiceTypeIndex.From([fixture]);

        var violations = CompositionRootRules.CheckServiceConstructionOutsideRoot(fixture, index, NoRoots).ToList();

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
