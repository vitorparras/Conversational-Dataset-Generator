using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Core.Platform.Tests.Logging;
using Core.Platform.Versions;

namespace Core.Platform.Tests.Versions;

/// <summary>
/// A temporary directory holding a probe project that imports the build stamp: committed in a git repository of its
/// own, or outside any repository. git never looks above the directory, so where the temporary path lies changes nothing.
/// </summary>
internal sealed class ProbeWorkspace : IDisposable
{
    private const string ProjectName = "BuildInfoProbe";
    private const int DeleteAttempts = 5;
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DeletePause = TimeSpan.FromMilliseconds(200);
    private static readonly Lazy<string> CoreDirectory = new(FindCoreDirectory);

    private readonly string path = Path.Combine(Path.GetTempPath(), $"cdg-test-{Guid.NewGuid():N}");

    private ProbeWorkspace()
    {
        _ = Directory.CreateDirectory(path);
        File.Copy(
            Path.Combine(CoreDirectory.Value, "tests", "fixtures", "build-info", ProjectName + ".csproj.txt"),
            Path.Combine(path, ProjectName + ".csproj"));
    }

    /// <summary>The probe in a directory that belongs to no git repository.</summary>
    public static ProbeWorkspace OutsideAnyRepository() => new();

    /// <summary>A repository whose single commit holds the probe and ignores the build output, so a build leaves it clean.</summary>
    public static async Task<ProbeWorkspace> CommittedAsync(CancellationToken cancellationToken)
    {
        var workspace = new ProbeWorkspace();
        await File.WriteAllTextAsync(Path.Combine(workspace.path, ".gitignore"), "bin/\nobj/\n", cancellationToken);
        _ = await workspace.GitAsync(cancellationToken, "init", "--quiet");
        _ = await workspace.GitAsync(cancellationToken, "add", "--all");
        _ = await workspace.GitAsync(
            cancellationToken, "-c", "user.name=PESSOA_1", "-c", "user.email=pessoa1@example.invalid", "-c", "commit.gpgsign=false",
            "commit", "--quiet", "--no-verify", "--message", "probe");
        return workspace;
    }

    /// <summary>Leaves a file git does not know about, as an edit not yet committed would.</summary>
    public void LeaveUncommittedChange() => File.WriteAllText(Path.Combine(path, "ZQX-UNCOMMITTED.txt"), "ZQX-MARKER-0929");

    public async Task<string> HeadAsync(CancellationToken cancellationToken) =>
        (await GitAsync(cancellationToken, "rev-parse", "HEAD")).Trim();

    /// <summary>Builds the probe with the search path given to the build, and reads the stamp the build left on it.</summary>
    public async Task<ProbeBuild> BuildAsync(string searchPath, CancellationToken cancellationToken)
    {
        var build = await ExternalProcess.RunAsync(BuildStartInfo(searchPath), Timeout, cancellationToken);
        Assert.True(build.ExitCode == 0, build.Output);
        // Loaded from memory, so the file is not held open and the directory can be deleted on every platform. Every build
        // is the same assembly name with another stamp, so each gets a context of its own; the contexts are not collectible,
        // because unloading them while other tests allocate is what the runtime's collector did not survive.
        using var image = new MemoryStream(await File.ReadAllBytesAsync(
            Path.Combine(path, "bin", "Debug", "net10.0", ProjectName + ".dll"), cancellationToken));
        var probe = new AssemblyLoadContext(ProjectName, isCollectible: false).LoadFromStream(image);
        return new ProbeBuild(
            BuildInfo.Of(probe), [.. probe.GetCustomAttributes<AssemblyMetadataAttribute>().Select(attribute => attribute.Key)]);
    }

    /// <summary>Deletes the directory, retrying while a file is still held or read-only, and gives up quietly after the last attempt.</summary>
    public void Dispose()
    {
        for (var attempt = 1; attempt <= DeleteAttempts && Directory.Exists(path); attempt++)
        {
            TryDelete();
        }
    }

    private void TryDelete()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
            Thread.Sleep(DeletePause);
        }
        catch (UnauthorizedAccessException)
        {
            Thread.Sleep(DeletePause);
        }
    }

    private async Task<string> GitAsync(CancellationToken cancellationToken, params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = path };
        info.Environment["GIT_CEILING_DIRECTORIES"] = Path.GetDirectoryName(path);
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        var outcome = await ExternalProcess.RunAsync(info, Timeout, cancellationToken);
        Assert.True(outcome.ExitCode == 0, outcome.Output);
        return outcome.Output;
    }

    /// <summary>dotnet is named by its full path, so the build starts even when the search path is empty.</summary>
    private ProcessStartInfo BuildStartInfo(string searchPath)
    {
        var dotnet = Path.GetFullPath(Path.Combine(
            RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
        var info = new ProcessStartInfo(dotnet) { WorkingDirectory = path };
        info.Environment["PATH"] = searchPath;
        info.Environment["GIT_CEILING_DIRECTORIES"] = Path.GetDirectoryName(path);
        foreach (var argument in new[] { "build", "-nologo", "-nodeReuse:false", "-p:UseSharedCompilation=false" })
        {
            info.ArgumentList.Add(argument);
        }

        info.ArgumentList.Add($"-p:BuildInfoTargets={Path.Combine(CoreDirectory.Value, "build", "BuildInfo.targets")}");
        return info;
    }

    private static string FindCoreDirectory()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Core.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException("Core.slnx was not found above the test output.");
    }
}
