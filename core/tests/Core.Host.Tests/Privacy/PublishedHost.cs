using System.Diagnostics;

namespace Core.Host.Tests.Privacy;

/// <summary>The host published into a folder of its own, the way the package step publishes it, from the sources of the working copy.</summary>
internal static class PublishedHost
{
    private const string Project = "core/src/Core.Host/Core.Host.csproj";
    private static readonly TimeSpan Limit = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Publishes into the folder and returns the full path of every file it wrote; a publish that fails fails with its cause and the end of what it said (PublishFailure).
    /// It builds in a tree of its own (obj and bin under a temporary artifacts path, restored in locked mode), so that nothing it writes is read or rewritten
    /// by the rest of the run, which uses the obj and bin of core/src at the same time.
    /// </summary>
    public static async Task<IReadOnlyList<string>> IntoAsync(string folder, CancellationToken cancellationToken)
    {
        using var tree = new TemporaryDirectory();
        var info = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = RepositoryFiles.Root(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        string[] arguments =
        [
            "publish", Project, "--artifacts-path", tree.Path, "-p:RestoreLockedMode=true", "--configuration", "Release",
            "--output", folder, "-nodeReuse:false", "-p:UseSharedCompilation=false",
        ];
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var publish = Process.Start(info) ?? throw new InvalidOperationException("dotnet did not start.");
        var said = Task.WhenAll(publish.StandardOutput.ReadToEndAsync(cancellationToken), publish.StandardError.ReadToEndAsync(cancellationToken));
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(Limit);
        try
        {
            await publish.WaitForExitAsync(limit.Token);
        }
        finally
        {
            if (!publish.HasExited)
            {
                publish.Kill(entireProcessTree: true);
            }
        }

        var streams = await said;
        return publish.ExitCode == 0
            ? [.. Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)]
            : throw new InvalidOperationException(PublishFailure.Describe(publish.ExitCode, streams[0], streams[1]));
    }
}
