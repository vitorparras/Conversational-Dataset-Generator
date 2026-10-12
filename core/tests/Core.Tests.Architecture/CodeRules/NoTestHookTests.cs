namespace Core.Tests.Architecture.CodeRules;

/// <summary>Code rule 4 over every production project and file, with one fixture that breaks each part of it.</summary>
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
            .SelectMany(project => SourceTree.ProductionFiles()
                .Where(file => file.Path.StartsWith(Path.GetDirectoryName(project.Path)!, StringComparison.Ordinal))
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

        Assert.Contains("Other.Tests.Unit", violation.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AttributeExposingInternalsToAForeignAssemblyIsRefused()
    {
        var fixture = SourceTree.Fixture("InternalsVisibleToForeign.cs");

        var violation = Assert.Single(NoTestHookRules.CheckInternalsVisibleTo(fixture, "Core.Sample"));

        Assert.Equal(4, violation.Line);
    }

    [Fact]
    public void PublicMemberNamedForATestIsRefused()
    {
        var fixture = SourceTree.Fixture("PublicTestingMember.cs");

        var violations = NoTestHookRules.CheckPublicMemberNames(fixture).ToList();

        Assert.Equal([8, 10, 12], violations.Select(violation => violation.Line));
    }
}
