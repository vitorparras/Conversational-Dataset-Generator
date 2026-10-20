namespace Core.Tests.Architecture.CodeRules;

/// <summary>The build files of the repository read no signal of a guard's build and filter no package asset, with a fixture that names each signal.</summary>
public sealed class BuildFileRulesTests
{
    private const string Fixture = "build-files/EverySignal.props.txt";

    [Fact]
    public void RepositoryBuildFilesReadNoSignalOfTheGuard()
    {
        var violations = BuildFileRules.Check(Path.GetDirectoryName(SourceTree.CoreRoot)!);

        Assert.Empty(violations);
    }

    [Theory]
    [InlineData(2, "o atributo Condition de PropertyGroup lê ArtifactsPath")]
    [InlineData(5, "o atributo Condition de PropertyGroup lê artifactspath")]
    [InlineData(9, "o atributo Condition de Quiet lê SolutionDir")]
    [InlineData(10, "o atributo Condition de Quiet lê TESTINGPLATFORM_TELEMETRY_OPTOUT")]
    [InlineData(11, "o atributo Condition de Quiet lê GetTempPath")]
    [InlineData(12, "o valor de Quiet lê canary")]
    [InlineData(15, "PackageReference de Microsoft.CodeAnalysis.BannedApiAnalyzers declara ExcludeAssets")]
    [InlineData(16, "PackageReference de SonarAnalyzer.CSharp declara IncludeAssets")]
    public void BuildFileThatCanTellTheGuardFromTheGateIsRefusedNamingLineAndSignal(int line, string message)
    {
        var path = Path.Combine(SourceTree.FixturesDirectory, Fixture);

        var violations = BuildFileRules.CheckFile(path);

        Assert.Contains(violations, violation => violation.Line == line && violation.Message.StartsWith(message, StringComparison.Ordinal));
    }
}
