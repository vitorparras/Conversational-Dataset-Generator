namespace Core.Platform.Tests.Logging;

/// <summary>A message string handed to the logger, by extension method or by the interface itself, does not build.</summary>
public sealed class BannedLoggingBuildTests
{
    private const string Fixture = "logging-banned";
    private const string Project = "LoggingBanned.csproj";
    private const string BannedSymbol = "error RS0030";

    [Fact]
    public async Task InterpolatedMessageThroughTheExtensionMethodFailsTheBuild()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var build = await DotnetBuild.OfFixtureAsync(Fixture, Project, cancellationToken);

        Assert.NotEqual(0, build.ExitCode);
        Assert.Contains(build.Lines, line => line.Contains("ExtensionRoute.cs", StringComparison.Ordinal) && line.Contains(BannedSymbol, StringComparison.Ordinal));
    }

    [Fact]
    public async Task MessageThroughTheInterfaceMethodFailsTheBuild()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var build = await DotnetBuild.OfFixtureAsync(Fixture, Project, cancellationToken);

        Assert.NotEqual(0, build.ExitCode);
        Assert.Contains(build.Lines, line => line.Contains("InterfaceRoute.cs", StringComparison.Ordinal) && line.Contains(BannedSymbol, StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheBannedSymbolIsTheOnlyReasonTheFixtureFails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var build = await DotnetBuild.OfFixtureAsync(Fixture, Project, cancellationToken);

        var errors = build.Lines.Where(line => line.Contains(": error ", StringComparison.Ordinal));
        Assert.All(errors, line => Assert.Contains(BannedSymbol, line, StringComparison.Ordinal));
    }
}
