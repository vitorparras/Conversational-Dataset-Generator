namespace Core.Tests.Architecture.CodeRules;

/// <summary>One project's <c>msbuild</c> runs, with everything they write sent to a scratch directory and no process left behind.</summary>
internal sealed class MsBuildRun
{
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

    public string GeneratedFiles => Path.Combine(scratch, "generated");

    public CommandOutcome Execute(params string[] extra) =>
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
