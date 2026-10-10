using System.Diagnostics;
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

        var (exitCode, output) = await BuildAsync(project);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Leak.cs(9,62): error RS0030", output, StringComparison.Ordinal);
        Assert.Contains("LoggerExtensions.LogInformation", output, StringComparison.Ordinal);
    }

    private static async Task<(int ExitCode, string Output)> BuildAsync(string project)
    {
        var info = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(project)!,
        };
        foreach (var argument in new[] { "build", project, "-nologo", "-nodeReuse:false", "-p:UseSharedCompilation=false" })
        {
            info.ArgumentList.Add(argument);
        }

        // The test host runs inside MSBuild's environment; a nested build must not inherit it.
        foreach (var name in Environment.GetEnvironmentVariables().Keys.Cast<string>())
        {
            if (name.StartsWith("MSBUILD", StringComparison.OrdinalIgnoreCase))
            {
                _ = info.Environment.Remove(name);
            }
        }

        using var process = Process.Start(info)!;
        var standardOutput = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        return (process.ExitCode, await standardOutput + await standardError);
    }
}
