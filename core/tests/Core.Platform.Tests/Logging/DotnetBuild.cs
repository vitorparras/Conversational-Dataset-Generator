using System.Diagnostics;

namespace Core.Platform.Tests.Logging;

/// <summary>A build of one fixture project under <c>core/tests/fixtures</c>, run as the operator would run it.</summary>
internal static class DotnetBuild
{
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    private static readonly Lazy<string> CoreDirectory = new(FindCoreDirectory);

    public static Task<ProcessOutcome> OfFixtureAsync(string fixtureDirectory, string projectFile, CancellationToken cancellationToken) =>
        ExternalProcess.RunAsync(StartInfo(Path.Combine(CoreDirectory.Value, "tests", "fixtures", fixtureDirectory, projectFile)), Timeout, cancellationToken);

    /// <summary>No node reuse and no shared compiler, so that nothing of the build outlives the test.</summary>
    private static ProcessStartInfo StartInfo(string projectPath)
    {
        var info = new ProcessStartInfo("dotnet") { WorkingDirectory = Path.GetDirectoryName(projectPath) };
        info.ArgumentList.Add("build");
        info.ArgumentList.Add(projectPath);
        info.ArgumentList.Add("-nologo");
        info.ArgumentList.Add("-nodeReuse:false");
        info.ArgumentList.Add("-p:UseSharedCompilation=false");
        return info;
    }

    private static string FindCoreDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Core.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Core.slnx was not found above the test output.");
    }
}
