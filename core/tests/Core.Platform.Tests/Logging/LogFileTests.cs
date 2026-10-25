using Core.Platform.Logging;
using Core.Platform.Tests.Sources;

namespace Core.Platform.Tests.Logging;

/// <summary>The open file takes one writer at a time, flushes each text and drops what comes after it is closed.</summary>
public sealed class LogFileTests
{
    private const int Writers = 8;
    private const int LinesPerWriter = 2000;

    [Fact]
    public void TextIsOnDiskAsSoonAsItIsAppended()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "app.log");
        using var writer = Writer(path);
        using var file = new LogFile(writer);

        file.Append("ZQX-LINE-7731\n");

        Assert.Equal("ZQX-LINE-7731\n", SharedFile.Read(path));
    }

    [Fact]
    public void ConcurrentWritersNeverInterleaveALine()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "app.log");
        using var writer = Writer(path);

        using (var file = new LogFile(writer))
        {
            AppendFromManyThreads(file);
        }

        Assert.Equal(ExpectedLines(), File.ReadAllLines(path).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void TextAppendedAfterDisposeIsDroppedWithoutThrowing()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "app.log");
        using var writer = Writer(path);
        var file = new LogFile(writer);
        file.Append("ZQX-BEFORE-7731\n");
        file.Dispose();

        file.Append("ZQX-AFTER-7731\n");

        Assert.Equal("ZQX-BEFORE-7731\n", File.ReadAllText(path));
    }

    [Fact]
    public void DisposeClosesTheFile()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "app.log");
        using var writer = Writer(path);
        var file = new LogFile(writer);

        file.Dispose();

        Assert.True(SharedFile.IsClosed(path));
    }

    private static StreamWriter Writer(string path) =>
        new(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read));

    /// <summary>Every writer on its own thread, all released at once so that they contend from the first line.</summary>
    private static void AppendFromManyThreads(LogFile file)
    {
        using var start = new Barrier(Writers);
        var writers = Enumerable.Range(0, Writers)
            .Select(writer => Task.Factory.StartNew(
                () => AppendAfter(start, file, writer), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default))
            .ToArray();
        Task.WaitAll(writers, TestContext.Current.CancellationToken);
    }

    private static void AppendAfter(Barrier start, LogFile file, int writer)
    {
        start.SignalAndWait(TestContext.Current.CancellationToken);
        foreach (var index in Enumerable.Range(writer * LinesPerWriter, LinesPerWriter))
        {
            file.Append(Line(index) + "\n");
        }
    }

    private static IEnumerable<string> ExpectedLines() =>
        Enumerable.Range(0, Writers * LinesPerWriter).Select(Line).Order(StringComparer.Ordinal);

    private static string Line(int index) => $"ZQX-LINE-{index:D5}-" + new string('x', 300);
}
