using Core.Tests.Shared;

namespace Core.Tests.Unit.Platform.Logging;

/// <summary>
/// Issue 34 CA-3: a value interpolated straight into a log message fails the build. The fixture
/// under core/tests/Fixtures is outside the solution and is compiled here, with the same rules.
/// </summary>
public sealed class InterpolatedLoggingBuildTests
{
    [Fact]
    public async Task InterpolatedLogCallFailsTheBuildWithTheBannedApiRule()
    {
        var project = Path.Combine(
            RepositoryRoot.Find().FullName, "core", "tests", "Fixtures", "InterpolatedLogging", "InterpolatedLogging.csproj");

        var (exitCode, output) = await ChildProcess.RunAsync(
            ChildProcess.Dotnet,
            ["build", project, "-nologo", "-nodeReuse:false", "-p:UseSharedCompilation=false"],
            Path.GetDirectoryName(project)!);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Leak.cs(9,62): error RS0030", output, StringComparison.Ordinal);
        Assert.Contains("LoggerExtensions.LogInformation", output, StringComparison.Ordinal);
    }
}
