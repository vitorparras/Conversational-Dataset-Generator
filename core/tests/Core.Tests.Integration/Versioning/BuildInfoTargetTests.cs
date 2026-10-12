using System.Security;
using System.Text.RegularExpressions;
using Core.Platform.Versioning;
using Core.Tests.Shared;

namespace Core.Tests.Integration.Versioning;

/// <summary>
/// Issue 51 CA-5 and issue 46 CA-4 on the real build target, run in repositories made for the
/// test: a clean tree reads false with the exact commit, a dirty one reads true, and a directory
/// outside any repository, or a machine without git, reads absent for both.
/// </summary>
public sealed partial class BuildInfoTargetTests : IDisposable
{
    private const string ProjectFile = "BuildInfoProbe.proj";

    private static readonly string[] Identity =
    [
        "-c", "user.name=ZQX-MARKER-7731", "-c", "user.email=zqx-marker-7731@example.invalid",
        "-c", "commit.gpgsign=false", "-c", "init.defaultBranch=main",
    ];

    private readonly DirectoryInfo root = Directory.CreateTempSubdirectory("cdg-buildinfo-");

    public void Dispose()
    {
        // Git writes its objects read-only; Windows refuses to delete them otherwise.
        foreach (var file in root.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            file.Attributes = FileAttributes.Normal;
        }

        root.Delete(recursive: true);
    }

    [Fact]
    public async Task CleanTreeReadsFalseWithTheExactCommit()
    {
        var repository = await CommittedRepositoryAsync();
        var head = await HeadAsync(repository);

        var generated = await RunTargetAsync(repository);

        Assert.Equal(VersionValue.Of(head), CommitOf(generated));
        Assert.Equal(VersionValue.Of("false"), DirtyTreeOf(generated));
    }

    [Fact]
    public async Task ChangedTrackedFileReadsDirty()
    {
        var repository = await CommittedRepositoryAsync();
        var head = await HeadAsync(repository);
        await File.AppendAllTextAsync(
            Path.Combine(repository, "tracked.txt"), "changed\n", TestContext.Current.CancellationToken);

        var generated = await RunTargetAsync(repository);

        Assert.Equal(VersionValue.Of(head), CommitOf(generated));
        Assert.Equal(VersionValue.Of("true"), DirtyTreeOf(generated));
    }

    [Fact]
    public async Task DirectoryOutsideAnyRepositoryReadsAbsentAndKeepsGitErrorOut()
    {
        var directory = root.CreateSubdirectory("no-repository").FullName;
        WriteProject(directory);

        var generated = await RunTargetAsync(directory);

        Assert.NotEqual("0", generated["CommitExitCode"]);
        Assert.Equal(string.Empty, generated["CommitOutput"]);
        Assert.Same(Absent.Instance, CommitOf(generated));
        Assert.Same(Absent.Instance, DirtyTreeOf(generated));
    }

    [Fact]
    public async Task MachineWithoutGitReadsAbsent()
    {
        var repository = await CommittedRepositoryAsync();

        // A search path with nothing on it: the build runs, and git cannot be found.
        var generated = await RunTargetAsync(repository, root.CreateSubdirectory("empty-path").FullName);

        Assert.NotEqual("0", generated["CommitExitCode"]);
        Assert.Same(Absent.Instance, CommitOf(generated));
        Assert.Same(Absent.Instance, DirtyTreeOf(generated));
    }

    private static VersionValue CommitOf(Dictionary<string, string> generated) =>
        BuildInfo.CommitFrom(generated["CommitExitCode"], generated["CommitOutput"]);

    private static VersionValue DirtyTreeOf(Dictionary<string, string> generated) =>
        BuildInfo.DirtyTreeFrom(generated["StatusExitCode"], generated["StatusOutputLength"]);

    [GeneratedRegex("""public const string (\w+) = "(.*)";""")]
    private static partial Regex Constant();

    private static void WriteProject(string directory)
    {
        var targets = Path.Combine(RepositoryRoot.Find().FullName, "core", "build", "BuildInfo.targets");
        File.WriteAllText(
            Path.Combine(directory, ProjectFile),
            $"""
            <Project>
              <PropertyGroup>
                <Version>0.0.0-zqx</Version>
                <IntermediateOutputPath>obj/</IntermediateOutputPath>
              </PropertyGroup>
              <Import Project="{SecurityElement.Escape(targets)}" />
            </Project>
            """);
    }

    /// <summary>Git reads only what the test gives it: no system or user configuration, no parent repository.</summary>
    private Dictionary<string, string?> GitEnvironment()
    {
        var configuration = Path.Combine(root.FullName, "empty.gitconfig");
        if (!File.Exists(configuration))
        {
            File.WriteAllText(configuration, string.Empty);
        }

        return new()
        {
            ["GIT_CONFIG_NOSYSTEM"] = "1",
            ["GIT_CONFIG_GLOBAL"] = configuration,
            ["GIT_CEILING_DIRECTORIES"] = root.FullName,
        };
    }

    private async Task<string> CommittedRepositoryAsync()
    {
        var repository = root.CreateSubdirectory("repository").FullName;
        WriteProject(repository);
        var token = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(Path.Combine(repository, ".gitignore"), "obj/\n", token);
        await File.WriteAllTextAsync(Path.Combine(repository, "tracked.txt"), "ZQX-MARKER-7731\n", token);
        _ = await GitAsync(repository, "init", "--quiet");
        _ = await GitAsync(repository, "add", "--all");
        _ = await GitAsync(repository, "commit", "--quiet", "--message", "probe");
        return repository;
    }

    private async Task<string> HeadAsync(string repository) =>
        (await GitAsync(repository, "rev-parse", "HEAD")).Trim();

    private async Task<string> GitAsync(string directory, params string[] arguments)
    {
        var (exitCode, output) = await ChildProcess.RunAsync(
            "git", [.. Identity, .. arguments], directory, GitEnvironment());
        Assert.True(exitCode == 0, $"git {string.Join(' ', arguments)} failed:\n{output}");
        return output;
    }

    /// <summary>Runs only the target, then reads the constants it generated.</summary>
    private async Task<Dictionary<string, string>> RunTargetAsync(string directory, string? searchPath = null)
    {
        var environment = GitEnvironment();
        if (searchPath is not null)
        {
            environment["PATH"] = searchPath;
            if (!OperatingSystem.IsWindows())
            {
                // MSBuild runs Exec through the shell, which it looks for on the search path; the shell
                // must be findable for the build to run at all, and git must not be.
                _ = File.CreateSymbolicLink(Path.Combine(searchPath, "sh"), "/bin/sh");
            }
        }

        var (exitCode, output) = await ChildProcess.RunAsync(
            ChildProcess.Dotnet,
            ["msbuild", ProjectFile, "-t:GenerateBuildInfo", "-nologo", "-nodeReuse:false", "-v:quiet"],
            directory,
            environment);
        Assert.True(exitCode == 0, "the target failed:\n" + output);

        var generated = await File.ReadAllTextAsync(
            Path.Combine(directory, "obj", "BuildInfo.g.cs"), TestContext.Current.CancellationToken);
        return Constant().Matches(generated).ToDictionary(match => match.Groups[1].Value, match => match.Groups[2].Value);
    }
}
