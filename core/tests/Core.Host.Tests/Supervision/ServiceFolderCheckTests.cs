using Core.Host.Supervision;
using Core.Platform.Startup;

namespace Core.Host.Tests.Supervision;

/// <summary>Before the service starts, the folder is checked against its manifest; a manifest or a file missing, or a sum that differs, refuses the start naming the file.</summary>
public sealed class ServiceFolderCheckTests : IDisposable
{
    private const string Hash = "0000000000000000000000000000000000000000000000000000000000000000";

    private readonly MountedServiceFolder mounted = new();

    private ServiceFolderCheck Check => new(mounted.Folder, given: null);

    [Fact]
    public async Task FolderThatMatchesItsManifestPasses()
    {
        var result = await Check.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Passed, result.Status);
    }

    [Fact]
    public async Task TwoChecksInARowOnTheSameFolderStayPassed()
    {
        _ = await Check.RunAsync(TestContext.Current.CancellationToken);

        var second = await Check.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Passed, second.Status);
    }

    [Fact]
    public async Task ManifestThatIsMissingFailsNamingTheManifest()
    {
        File.Delete(mounted.Folder.Manifest);

        var result = await Check.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("language-service/SHA256SUMS", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FileThatIsMissingFailsNamingTheFile()
    {
        File.Delete(Path.Combine(mounted.Folder.Root, MountedServiceFolder.ModulePath));

        var result = await Check.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains($"language-service/{MountedServiceFolder.ModulePath}", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FileThatWasChangedFailsNamingTheFile()
    {
        mounted.Write(MountedServiceFolder.ModulePath, "module changed");

        var result = await Check.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains($"language-service/{MountedServiceFolder.ModulePath}", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LineOutsideTheManifestFormatFailsNamingTheManifest()
    {
        mounted.Rewrite("not a sum\n");

        var result = await Check.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("language-service/SHA256SUMS", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PathThatLeavesTheFolderFails()
    {
        mounted.Rewrite($"{Hash}  ../outside.bin\n");

        var result = await Check.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("../outside.bin", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecutableGivenByTheConfigurationSkipsTheFolderAndSaysSo()
    {
        File.Delete(mounted.Folder.Manifest);
        var check = new ServiceFolderCheck(mounted.Folder, given: "zqx-given-7731");

        var result = await check.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Passed, result.Status);
        Assert.Contains("não foi conferida", result.Message, StringComparison.Ordinal);
        Assert.Equal(LanguageServiceConfiguration.ExecutablePath, result.ExpectedSource);
    }

    public void Dispose() => mounted.Dispose();
}
