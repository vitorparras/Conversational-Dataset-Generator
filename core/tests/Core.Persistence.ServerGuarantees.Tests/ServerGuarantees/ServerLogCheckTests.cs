using Core.Persistence.ServerGuarantees;
using Core.Platform.Startup;
using Core.Tests.Support.Audit;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>The folder named for the server's log must exist and hold a log file the process can read; each absence is a refusal that names the check and says what is missing.</summary>
public sealed class ServerLogCheckTests : IDisposable
{
    private readonly ServerLogFolder folder = new();

    [Fact]
    public async Task FolderWithALogFileAndNoWritePermissionPasses()
    {
        _ = folder.Write("postgresql-a.csv", DateTimeOffset.UtcNow, "x");
        ServerLogFiles.Seal(folder.Path);

        var result = await new ServerLogCheck(folder.Path).RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal((CheckStatus.Passed, "ServerLog"), (result.Status, result.Name));
    }

    [Fact]
    public async Task FolderTheProcessCanWriteInIsRefusedNamingTheFolderAndTheAdjustmentAndLeavesItAsItWas()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux() && !Environment.IsPrivilegedProcess, "Só o Linux sem privilégio de administrador recusa a escrita pelo modo da pasta.");
        _ = folder.Write("postgresql-a.csv", DateTimeOffset.UtcNow, "x");
        var before = Directory.EnumerateFileSystemEntries(folder.Path).Order(StringComparer.Ordinal).ToArray();

        var result = await new ServerLogCheck(folder.Path).RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains(folder.Path, result.Message, StringComparison.Ordinal);
        Assert.Contains("gravável", result.Message, StringComparison.Ordinal);
        Assert.Contains("tire a escrita", result.Message, StringComparison.Ordinal);
        Assert.Contains("Audit:ServerLogDirectory", result.ExpectedSource, StringComparison.Ordinal);
        Assert.Equal(before, Directory.EnumerateFileSystemEntries(folder.Path).Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task FolderWithNoCsvFileIsRefused()
    {
        _ = folder.Write("other.txt", DateTimeOffset.UtcNow, "x");

        var result = await new ServerLogCheck(folder.Path).RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains(".csv", result.Message, StringComparison.Ordinal);
        Assert.Contains("Audit:ServerLogDirectory", result.ExpectedSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FolderThatDoesNotExistIsRefused()
    {
        var result = await new ServerLogCheck(Path.Combine(folder.Path, "zqx-missing")).RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("não pode ser lida", result.Message, StringComparison.Ordinal);
    }

    public void Dispose() => folder.Dispose();
}
