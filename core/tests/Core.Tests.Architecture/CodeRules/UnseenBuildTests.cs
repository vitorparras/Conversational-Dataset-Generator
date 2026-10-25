namespace Core.Tests.Architecture.CodeRules;

/// <summary>
/// The build the guards see and a build the build files cannot tell from the gate's give the compiler the same inputs and refuse the
/// same routes, with a route of the operator's that hides from the artifacts path and one written in a spelling no list knows.
/// </summary>
public sealed class UnseenBuildTests
{
    private const string Fixtures = "build-mirror";
    private const string SilencedProject = "Core.Platform";

    public static TheoryData<string> ProductionProjectNames() =>
        [.. SourceTree.ProductionProjects().Select(project => project.ModuleName)];

    [Theory]
    [MemberData(nameof(ProductionProjectNames))]
    public async Task BuildTheBuildFilesCannotTellFromTheGateMatchesTheGuardAndRefusesEveryRoute(string projectName)
    {
        var root = RepositoryRoot;
        using var mirror = GateMirror.Of(root);

        var unseen = await mirror.BuildAsync(projectName, TestContext.Current.CancellationToken);

        Assert.NotEmpty(unseen.Sites);
        Assert.Empty(UnseenBuildRules.Check(CompilerInvocation.Of(SourceTree.ProductionProject(projectName).Path), root, unseen));
    }

    [Fact]
    public async Task NoWarnAndRemovedSourceThatHideFromTheArtifactsPathAreRefusedByName()
    {
        using var mirror = Mirror("ArtifactsPathRoute");
        var seen = CompilerInvocation.Of(mirror.ProjectPath(SilencedProject));

        var unseen = await mirror.BuildAsync(SilencedProject, TestContext.Current.CancellationToken);
        var violations = UnseenBuildRules.Check(seen, mirror.Root, unseen).ToList();

        Assert.Empty(LoggingInvocationRules.Check(seen));
        Assert.Contains(violations, violation => violation.Message.StartsWith("opção de diagnóstico RS0030=Suppress entra no build igual ao do gate", StringComparison.Ordinal));
        Assert.Contains(violations, violation => violation.Message.StartsWith("fonte repositório:core/src/Core.Platform/Hidden.cs entra no build igual ao do gate", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildFileThatHidesFromTheArtifactsPathIsNamedWithItsCondition()
    {
        using var mirror = Mirror("ArtifactsPathRoute");

        var violations = BuildFileRules.Check(mirror.Root);

        Assert.Contains(violations, violation =>
            violation.File.EndsWith("Directory.Build.props", StringComparison.Ordinal)
            && violation.Message.StartsWith("o atributo Condition de PropertyGroup lê ArtifactsPath", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConditionInASpellingNoListKnowsIsStillRefusedByItsEffect()
    {
        using var mirror = Mirror("UnknownSpelling");
        var seen = CompilerInvocation.Of(mirror.ProjectPath(SilencedProject));

        var unseen = await mirror.BuildAsync(SilencedProject, TestContext.Current.CancellationToken);

        Assert.Empty(BuildFileRules.Check(mirror.Root));
        Assert.NotEmpty(unseen.Sites);
        Assert.Equal(unseen.Sites.Count * LoggingCanary.Routes.Count, UnseenBuildRules.CheckRoutesRefused(unseen).Count());
        Assert.Contains(UnseenBuildRules.CheckSameInputs(seen, mirror.Root, unseen), violation => violation.Message.StartsWith("opção de diagnóstico RS0030=Suppress", StringComparison.Ordinal));
    }

    private static string RepositoryRoot => Path.GetDirectoryName(SourceTree.CoreRoot)!;

    private static GateMirror Mirror(string fixture)
    {
        var mirror = GateMirror.Of(RepositoryRoot);
        mirror.Apply(Path.Combine(SourceTree.FixturesDirectory, Fixtures, fixture));
        return mirror;
    }
}
