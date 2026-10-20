namespace Core.Tests.Architecture.CodeRules;

/// <summary>
/// The analyzers of each production build come from the SDK or from accepted packages pinned by the lock, and the banned-symbol
/// diagnostic only from the pinned package; a fixture brings a package off the list and one that takes the pinned name.
/// </summary>
public sealed class AnalyzerPackageTests
{
    private const string Fixture = "analyzer-packages/Impostors";

    private static readonly Lazy<CompilerInvocation> Impostors = new(() => CompilerInvocation.OfFixture(Fixture, "App"));

    public static TheoryData<string> ProductionProjectNames() =>
        [.. SourceTree.ProductionProjects().Select(project => project.ModuleName)];

    [Theory]
    [MemberData(nameof(ProductionProjectNames))]
    public void ProductionAnalyzersComeFromTheSdkOrFromAcceptedPackagesPinnedByTheLock(string projectName)
    {
        var project = SourceTree.ProductionProject(projectName);

        var violations = AnalyzerPackageRules.CheckPackages(CompilerInvocation.Of(project.Path), LockOf(project.DirectoryPath));

        Assert.Empty(violations);
    }

    [Theory]
    [MemberData(nameof(ProductionProjectNames))]
    public void BannedSymbolOfAProductionBuildComesOnlyFromThePinnedPackage(string projectName)
    {
        var project = SourceTree.ProductionProject(projectName);

        var violations = AnalyzerPackageRules.CheckBannedSymbolEmitter(CompilerInvocation.Of(project.Path), LockOf(project.DirectoryPath));

        Assert.Empty(violations);
    }

    [Fact]
    public void AnalyzerPackageOffTheListIsRefusedByName()
    {
        var violations = AnalyzerPackageRules.CheckPackages(Impostors.Value, FixtureLock);

        Assert.Contains(violations, violation => violation.Message.StartsWith("o analisador Quiet.Analyzers.dll vem do pacote quiet.analyzers, que não está na lista", StringComparison.Ordinal));
    }

    [Fact]
    public void PackageThatTakesThePinnedNameIsRefusedByItsHash()
    {
        var violations = AnalyzerPackageRules.CheckPackages(Impostors.Value, FixtureLock);

        Assert.Contains(violations, violation => violation.Message.StartsWith("o pacote microsoft.codeanalysis.bannedapianalyzers 1.0.0 no cache não é o que o lock fixa", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Quiet.Analyzers.dll")]
    [InlineData("Microsoft.CodeAnalysis.BannedApiAnalyzers.dll")]
    public void BannedSymbolFromAnAnalyzerOtherThanThePinnedPackageIsRefused(string analyzer)
    {
        var violations = AnalyzerPackageRules.CheckBannedSymbolEmitter(Impostors.Value, FixtureLock);

        Assert.Contains(violations, violation => violation.Message.StartsWith($"RS0030 é emitido por {analyzer}, que não é o pacote", StringComparison.Ordinal));
    }

    [Fact]
    public void AnalyzerFromAPathOfItsOwnIsRefused()
    {
        var invocation = CompilerInvocation.OfFixture("logging-invocation/OwnAnalyzer", "App");

        var violations = AnalyzerPackageRules.CheckPackages(invocation, FixtureLock);

        Assert.Contains(violations, violation => violation.Message.StartsWith("o analisador Own.Analyzer.dll vem de fora do SDK e do cache de pacotes", StringComparison.Ordinal));
    }

    private static string FixtureLock => Path.Combine(SourceTree.FixturesDirectory, Fixture, "lock.json");

    private static string LockOf(string projectDirectory) => Path.Combine(projectDirectory, "packages.lock.json");
}
