namespace Core.Tests.Architecture.CodeRules;

/// <summary>No production code opens itself to tests, with fixtures that break each part of the rule.</summary>
public sealed class NoTestHookTests
{
    [Fact]
    public void ProjectFilesExposeInternalsOnlyToTheirOwnTests()
    {
        var violations = SourceTree.ProductionProjects().SelectMany(NoTestHookRules.CheckInternalsVisibleTo);

        Assert.Empty(violations);
    }

    [Fact]
    public void SourceFilesExposeInternalsOnlyToTheirOwnTests()
    {
        var violations = SourceTree.ProductionProjects()
            .SelectMany(project => CompilerInvocation.Of(project.Path).SourceFiles
                .SelectMany(file => NoTestHookRules.CheckInternalsVisibleTo(file, project.ModuleName)));

        Assert.Empty(violations);
    }

    [Fact]
    public void NoPublicMemberIsNamedForATest()
    {
        var violations = SourceTree.ProductionFiles().SelectMany(NoTestHookRules.CheckPublicMemberNames);

        Assert.Empty(violations);
    }

    [Fact]
    public void ProjectFileExposingInternalsToAForeignAssemblyIsRefused()
    {
        var project = ProjectFile.FromText("Core.Sample.csproj", SourceTree.FixtureText("ForeignInternals.csproj.txt"));

        var violation = Assert.Single(NoTestHookRules.CheckInternalsVisibleTo(project));

        Assert.Equal(4, violation.Line);
        Assert.Contains("Other.Tests", violation.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectFileExposingInternalsToTheTestProjectOfAnAreaOfTheModuleIsAcceptedAndToAnythingElseRefused()
    {
        var project = ProjectFile.FromText("Core.Sample.csproj", SourceTree.FixtureText("AreaInternals.csproj.txt"));

        var violations = NoTestHookRules.CheckInternalsVisibleTo(project).ToList();

        Assert.Equal([5, 6], violations.Select(violation => violation.Line));
    }

    [Fact]
    public void AttributeExposingInternalsToAForeignAssemblyIsRefused()
    {
        var fixture = SourceTree.Fixture("InternalsVisibleToForeign.cs");

        var violations = NoTestHookRules.CheckInternalsVisibleTo(fixture, "Core.Sample").ToList();

        Assert.Equal([5, 6], violations.Select(violation => violation.Line));
    }

    [Fact]
    public void PublicMemberNamedForATestIsRefused()
    {
        var fixture = SourceTree.Fixture("PublicTestingMember.cs");

        var violations = NoTestHookRules.CheckPublicMemberNames(fixture).ToList();

        Assert.Equal([8, 10, 12], violations.Select(violation => violation.Line));
    }
}
