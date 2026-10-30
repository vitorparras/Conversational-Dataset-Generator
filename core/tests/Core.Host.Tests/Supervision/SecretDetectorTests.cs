namespace Core.Host.Tests.Supervision;

/// <summary>The ways of looking for a leaked secret do see one when it is there, so an empty result means something.</summary>
public sealed class SecretDetectorTests
{
    private const string Marker = "ZQX-MARKER-7731-leak";

    [Trait("Category", "RealService")]
    [Fact]
    public async Task ProcessListingShowsASecretPassedAsAnArgument()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var process = new LeakingProcess(Marker);

        var lines = await ProcessTree.TreeOfAsync(process.Id, cancellationToken);

        Assert.Contains(lines, line => line.CommandLine.Contains(Marker, StringComparison.Ordinal));
    }

    [Fact]
    public void FileSweepNamesAFileThatHoldsTheSecret()
    {
        using var folder = new TemporaryDirectory();
        var path = Path.Combine(folder.Path, "leak.txt");
        File.WriteAllText(path, $"before {Marker} after");

        var files = SecretSweep.Holding(folder.Path, Marker, since: null);

        Assert.Equal([path], files);
    }

    [Fact]
    public void FileSweepNamesAHiddenFile()
    {
        using var folder = new TemporaryDirectory();
        var path = Path.Combine(folder.Path, ".leak");
        File.WriteAllText(path, Marker);
        File.SetAttributes(path, FileAttributes.Hidden);

        var files = SecretSweep.Holding(folder.Path, Marker, since: null);

        Assert.Equal([path], files);
    }

    [Fact]
    public void FileSweepNamesAFileInAHiddenFolder()
    {
        using var folder = new TemporaryDirectory();
        var hidden = Directory.CreateDirectory(Path.Combine(folder.Path, ".leaks"));
        hidden.Attributes |= FileAttributes.Hidden;
        var path = Path.Combine(hidden.FullName, "leak.txt");
        File.WriteAllText(path, Marker);

        var files = SecretSweep.Holding(folder.Path, Marker, since: null);

        Assert.Equal([path], files);
    }

    [Fact]
    public void FileSweepSkipsAFileWrittenBeforeTheMoment()
    {
        using var folder = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(folder.Path, "old.txt"), Marker);

        var files = SecretSweep.Holding(folder.Path, Marker, DateTimeOffset.UtcNow.AddHours(1));

        Assert.Empty(files);
    }

    [Fact]
    public void FileSweepNamesAFileWrittenAfterTheMarker()
    {
        using var marker = new SweepMarker();
        using var folder = new TemporaryDirectory();
        var path = Path.Combine(folder.Path, "new.txt");
        File.WriteAllText(path, Marker);

        var files = SecretSweep.Holding(folder.Path, Marker, marker.Since);

        Assert.Equal([path], files);
    }
}
