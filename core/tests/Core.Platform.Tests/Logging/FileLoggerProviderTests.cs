using Core.Platform.Logging;
using Core.Platform.Tests.Sources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Platform.Tests.Logging;

/// <summary>The file gets the console's line for every event, already redacted, each line on disk as soon as it is written.</summary>
[Collection("console")]
public sealed class FileLoggerProviderTests
{
    private const string Marker = "ZQX-MARKER-7731";
    private static readonly SampleItem Item = new("ZQX-ITEM-7731", Marker, "ZQX-PERSON-7731", "ZQX-TOKEN-7731");

    [Fact]
    public void FileLineIsTheConsoleLine()
    {
        using var pipeline = new FilePipeline();

        var written = pipeline.Logged(logger => logger.ItemFailed(new ItemReference("ZQX-ITEM-7731", "sanitize", "timeout"), Thrown()));

        Assert.Contains("ERROR Sample[1] Falha ao processar o item", written.File, StringComparison.Ordinal);
        Assert.Equal(written.Console, written.File);
    }

    [Fact]
    public void ClassifiedFieldsAreErasedFromTheFile()
    {
        using var pipeline = new FilePipeline();

        var written = pipeline.Logged(logger => logger.ItemSeen(Item));

        Assert.DoesNotContain(Marker, written.File, StringComparison.Ordinal);
        Assert.DoesNotContain("ZQX-PERSON-7731", written.File, StringComparison.Ordinal);
        Assert.DoesNotContain("ZQX-TOKEN-7731", written.File, StringComparison.Ordinal);
        Assert.Contains(" Text= ", written.File, StringComparison.Ordinal);
        Assert.Contains(" ItemId=ZQX-ITEM-7731", written.File, StringComparison.Ordinal);
    }

    [Fact]
    public void ClassifiedValueInterpolatedInTheTemplateIsErasedFromTheFile()
    {
        using var pipeline = new FilePipeline();

        var written = pipeline.Logged(logger => logger.TextShown(Marker));

        Assert.DoesNotContain(Marker, written.File, StringComparison.Ordinal);
        Assert.Contains("Sample[101] Texto ", written.File, StringComparison.Ordinal);
    }

    [Fact]
    public void EventAtLevelNoneIsNotWritten()
    {
        using var pipeline = new FilePipeline();

        var written = pipeline.Logged(logger => logger.AtLevel(LogLevel.None));

        Assert.Empty(written.File);
    }

    [Fact]
    public void LineIsOnDiskBeforeTheFileIsClosed()
    {
        using var pipeline = new FilePipeline();
        using var file = pipeline.Open();
        using var provider = FilePipeline.Build(file);

        provider.GetRequiredService<ILoggerFactory>().CreateLogger(FilePipeline.Category).ItemSeen(Item);

        Assert.Contains("Sample[100] Item visto", SharedFile.Read(pipeline.Path), StringComparison.Ordinal);
    }

    [Fact]
    public void DisposingTheProviderClosesTheFile()
    {
        using var pipeline = new FilePipeline();
        var file = pipeline.Open();

        file.Dispose();

        Assert.True(SharedFile.IsClosed(pipeline.Path));
    }

    [Fact]
    public void DisposingAPipelineThatBorrowsTheProviderLeavesTheFileOpen()
    {
        using var pipeline = new FilePipeline();
        using var file = pipeline.Open();

        FilePipeline.Build(file).Dispose();

        Assert.False(SharedFile.IsClosed(pipeline.Path));
    }

    [Fact]
    public void OpeningAppendsToWhatTheFileAlreadyHolds()
    {
        using var pipeline = new FilePipeline();
        File.WriteAllText(pipeline.Path, "ZQX-EARLIER-7731\n");

        var written = pipeline.Logged(logger => logger.ItemSeen(Item));

        Assert.StartsWith("ZQX-EARLIER-7731\n", written.File, StringComparison.Ordinal);
        Assert.Contains("Sample[100] Item visto", written.File, StringComparison.Ordinal);
    }

    [Fact]
    public void FileInAMissingDirectoryIsNotOpened()
    {
        using var directory = new TemporaryDirectory();

        using var file = FileLoggerProvider.Open(Path.Combine(directory.Path, "ZQX-MISSING-7731", "app.log"), new LineConsoleFormatter(TimeProvider.System), TextWriter.Null);

        Assert.Null(file);
    }

    [Fact]
    public void DirectoryInPlaceOfTheFileIsNotOpened()
    {
        using var directory = new TemporaryDirectory();

        using var file = FileLoggerProvider.Open(directory.Path, new LineConsoleFormatter(TimeProvider.System), TextWriter.Null);

        Assert.Null(file);
    }

    [Fact]
    public void PathWithAForbiddenCharacterIsNotOpened()
    {
        using var directory = new TemporaryDirectory();

        using var file = FileLoggerProvider.Open(Path.Combine(directory.Path, "ZQX-BAD-\u0000-7731.log"), new LineConsoleFormatter(TimeProvider.System), TextWriter.Null);

        Assert.Null(file);
    }

    [Fact]
    public void OpeningWithoutANoticeIsRefused()
    {
        using var pipeline = new FilePipeline();

        _ = Assert.Throws<ArgumentNullException>(() => FileLoggerProvider.Open(pipeline.Path, new LineConsoleFormatter(TimeProvider.System), null!));
    }

    private static InvalidOperationException Thrown()
    {
        try
        {
            throw new InvalidOperationException(Marker);
        }
        catch (InvalidOperationException exception)
        {
            return exception;
        }
    }
}
