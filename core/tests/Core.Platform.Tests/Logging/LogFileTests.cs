using Core.Platform.Logging;
using Core.Platform.Tests.Sources;

namespace Core.Platform.Tests.Logging;

/// <summary>The open file takes one writer at a time, flushes each text and drops what comes after it is closed or refused.</summary>
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
        using var file = new LogFile(writer, TextWriter.Null.WriteLine);

        file.Append("ZQX-LINE-7731\n");

        Assert.Equal("ZQX-LINE-7731\n", SharedFile.Read(path));
    }

    [Fact]
    public void ConcurrentWritersNeverInterleaveALine()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "app.log");
        using var writer = Writer(path);

        using (var file = new LogFile(writer, TextWriter.Null.WriteLine))
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
        var file = new LogFile(writer, TextWriter.Null.WriteLine);
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
        var file = new LogFile(writer, TextWriter.Null.WriteLine);

        file.Dispose();

        Assert.True(SharedFile.IsClosed(path));
    }

    [Fact]
    public void RefusedWriteIsNotThrownToWhoeverLogged()
    {
        using var stream = new RefusingStream();
        using var writer = new StreamWriter(stream);
        using var file = new LogFile(writer, TextWriter.Null.WriteLine);

        var thrown = Record.Exception(() => file.Append("ZQX-LINE-7731\n"));

        Assert.Null(thrown);
    }

    [Fact]
    public void RefusedWriteClosesTheFile()
    {
        using var stream = new RefusingStream();
        using var writer = new StreamWriter(stream);
        using var file = new LogFile(writer, TextWriter.Null.WriteLine);

        file.Append("ZQX-LINE-7731\n");

        Assert.True(stream.Closed);
    }

    [Fact]
    public void TextAppendedAfterARefusedWriteIsDroppedWithoutTryingTheFileAgain()
    {
        using var stream = new RefusingStream();
        using var writer = new StreamWriter(stream);
        using var file = new LogFile(writer, TextWriter.Null.WriteLine);
        file.Append("ZQX-FIRST-7731\n");

        file.Append("ZQX-SECOND-7731\n");

        Assert.Equal(1, stream.Writes);
    }

    [Fact]
    public void RefusedWriteSaysOnceOnTheNoticeThatTheFileStopped()
    {
        using var stream = new RefusingStream();
        using var writer = new StreamWriter(stream);
        using var notice = new StringWriter();
        using var file = new LogFile(writer, notice.WriteLine);

        file.Append("ZQX-FIRST-7731\n");
        file.Append("ZQX-SECOND-7731\n");

        var line = Assert.Single(notice.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        Assert.StartsWith("O registro em arquivo parou", line, StringComparison.Ordinal);
        Assert.EndsWith("O registro segue só no console.", line, StringComparison.Ordinal);
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
