using Core.Platform.Startup;
using Core.Platform.Tests.Sources;

namespace Core.Platform.Tests.Startup;

/// <summary>A file the installation must carry passes when present; when absent the result names the item and where it should come from.</summary>
public sealed class BundledFileCheckTests
{
    [Fact]
    public async Task PresentFilePassesAndNamesTheItem()
    {
        using var directory = new TemporaryDirectory();
        _ = directory.WriteFile("ZQX-MARKER-7731.bin", "content");
        var check = new BundledFileCheck("Sample:File", "ZQX-MARKER-7731.bin", directory.Path);

        var result = await check.Run(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Passed, result.Status);
        Assert.Equal("Sample:File", result.Name);
    }

    [Fact]
    public async Task AbsentFileFailsNamingTheItemAndTheExpectedSource()
    {
        using var directory = new TemporaryDirectory();
        var check = new BundledFileCheck("Sample:File", "ZQX-MARKER-7731.bin", directory.Path);

        var result = await check.Run(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Equal("Sample:File", result.Name);
        Assert.Contains("'Sample:File'", result.Message, StringComparison.Ordinal);
        Assert.Equal("ZQX-MARKER-7731.bin", result.ExpectedSource);
    }

    [Fact]
    public async Task DirectoryInPlaceOfTheFileFails()
    {
        using var directory = new TemporaryDirectory();
        _ = Directory.CreateDirectory(Path.Combine(directory.Path, "ZQX-MARKER-7731.bin"));
        var check = new BundledFileCheck("Sample:File", "ZQX-MARKER-7731.bin", directory.Path);

        var result = await check.Run(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Failed, result.Status);
    }
}
