namespace Core.Tests.Architecture.CodeRules;

/// <summary>Each way of silencing the banned-symbol analyzer from a build file lets the canary through and is refused.</summary>
public sealed class LoggingCanarySilencedByBuildFilesTests
{
    [Theory]
    [InlineData("NoWarn")]
    [InlineData("ConditionalNoWarn")]
    [InlineData("RemovedBannedSymbolsByName")]
    [InlineData("RemovedAnalyzerPackage")]
    public async Task SilencingTheAnalyzerLetsTheCanaryThroughAndIsRefused(string fixture)
    {
        var run = await CanaryRun.SilencedByAsync(fixture, TestContext.Current.CancellationToken);

        Assert.Empty(LoggingCanaryRules.CheckBuildIsIntact(run.Build));
        Assert.Equal(run.Sites.Count * LoggingCanary.Routes.Count, LoggingCanaryRules.CheckRoutesRefused(run.Sites, run.Build).Count());
    }
}
