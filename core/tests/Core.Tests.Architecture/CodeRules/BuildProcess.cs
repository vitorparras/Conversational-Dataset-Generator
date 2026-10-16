using System.Diagnostics;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>A <c>dotnet build</c> run to its end under a time limit; past the limit, or on any failure to wait, the whole process tree is killed.</summary>
internal static class BuildProcess
{
    public static readonly TimeSpan Limit = TimeSpan.FromMinutes(5);

    public static async Task<BuildOutcome> RunAsync(string projectPath, CancellationToken cancellationToken)
    {
        using var process = Process.Start(StartInfo(projectPath)) ?? throw new InvalidOperationException("The build did not start.");
        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var error = process.StandardError.ReadToEndAsync(CancellationToken.None);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(Limit);
        try
        {
            await process.WaitForExitAsync(limit.Token);
            return new BuildOutcome(process.ExitCode, await output + await error);
        }
        finally
        {
            Kill(process);
        }
    }

    /// <summary>No node reuse and no shared compiler, so that nothing of the build outlives the test.</summary>
    private static ProcessStartInfo StartInfo(string projectPath)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(projectPath),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        info.ArgumentList.Add("build");
        info.ArgumentList.Add(projectPath);
        info.ArgumentList.Add("-nologo");
        info.ArgumentList.Add("-nodeReuse:false");
        info.ArgumentList.Add("-p:UseSharedCompilation=false");
        return info;
    }

    private static void Kill(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }
    }
}
