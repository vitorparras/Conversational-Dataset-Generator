using System.Diagnostics;
using System.Globalization;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>One project's <c>msbuild</c> runs, with everything they write sent to a scratch directory and no process left behind.</summary>
internal sealed class MsBuildRun
{
    private static readonly Lock Writing = new();

    private readonly string projectPath;
    private readonly string scratch;
    private readonly string? packages;

    public MsBuildRun(string projectPath, string scratch, string? packages)
    {
        this.projectPath = projectPath;
        this.scratch = scratch;
        this.packages = packages;
    }

    public string Artifacts => Path.Combine(scratch, "artifacts");

    /// <summary>The run, timed: project, target and seconds go to the error stream and to the file ARCHITECTURE_MSBUILD_LOG names, so that a slow log says which msbuild was slow.</summary>
    public CommandOutcome Execute(params string[] extra)
    {
        var clock = Stopwatch.StartNew();
        var outcome = Run(extra);
        var line = string.Create(CultureInfo.InvariantCulture, $"[msbuild] {Path.GetFileName(projectPath)} {extra[0]} {clock.Elapsed.TotalSeconds:F1}s");
        Console.Error.WriteLine(line);
        if (Environment.GetEnvironmentVariable("ARCHITECTURE_MSBUILD_LOG") is { Length: > 0 } log)
        {
            lock (Writing)
            {
                File.AppendAllText(log, line + Environment.NewLine);
            }
        }

        return outcome;
    }

    private CommandOutcome Run(string[] extra) =>
        DotnetCommand.RunAsync(
            [
                "msbuild", projectPath, "-nologo", "-nodeReuse:false",
                "-p:UseSharedCompilation=false",
                "-p:RestoreLockedMode=false",
                $"-p:NuGetLockFilePath={Path.Combine(scratch, "packages.lock.json")}",
                $"-p:ArtifactsPath={Artifacts}",
                .. extra,
            ],
            Path.GetDirectoryName(projectPath)!,
            CancellationToken.None,
            packages is null ? null : new Dictionary<string, string> { ["NUGET_PACKAGES"] = packages }).GetAwaiter().GetResult();
}
