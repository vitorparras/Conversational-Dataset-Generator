using Core.Host.Tests.Logging;
using Core.Host.Tests.Supervision;

namespace Core.Host.Tests.Privacy;

/// <summary>
/// The sweep does fail: a marker written to the log by the composed host in a field no one classified, or by a child
/// process to its error stream, or to a file, is reported with the destination and the line that holds it
/// (issue #251, CA-2). The runs that wrote no file besides the log file leave the shared folders unread.
/// </summary>
[Collection("console")]
public sealed class LogSweepReprovesTests
{
    [Fact]
    public void MarkerInAnUnclassifiedFieldIsReportedOnTheConsoleAsStdout()
    {
        var marker = LogMarker.Fresh();
        using var composed = new ComposedFile();
        Seed(composed, marker);

        var leaks = LogSweep.Leaks(new LogEvidence(marker, composed.ConsoleText, string.Empty, LogFile: null, Since: null));

        var leak = Assert.Single(leaks);
        Assert.Equal(LogSweep.Stdout, leak.Destination);
        Assert.Contains($"[101] Item sem classificação Text={marker}", leak.Line, StringComparison.Ordinal);
    }

    [Fact]
    public void MarkerInAnUnclassifiedFieldIsReportedInTheFileAsHostLogFile()
    {
        var marker = LogMarker.Fresh();
        using var composed = new ComposedFile();
        Seed(composed, marker);

        var leaks = LogSweep.Leaks(new LogEvidence(marker, string.Empty, string.Empty, composed.Path, Since: null));

        var leak = Assert.Single(leaks);
        Assert.Equal(LogSweep.LogFile, leak.Destination);
        Assert.Contains($"[101] Item sem classificação Text={marker}", leak.Line, StringComparison.Ordinal);
    }

    [Fact]
    public void MarkerWrittenByAChildProcessToItsErrorStreamIsReportedAsStderr()
    {
        var marker = LogMarker.Fresh();
        var error = ChildProcessError.Written(marker);

        var leaks = LogSweep.Leaks(new LogEvidence(marker, string.Empty, error, LogFile: null, Since: null));

        var leak = Assert.Single(leaks);
        Assert.Equal(LogSweep.Stderr, leak.Destination);
        Assert.Contains(marker, leak.Line, StringComparison.Ordinal);
    }

    [Fact]
    public void MarkerWrittenToAFileDuringTheRunIsReportedByThePathOfTheFile()
    {
        var marker = LogMarker.Fresh();
        using var beginning = new SweepMarker();
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "ZQX-LEAK-0028.txt");
        File.WriteAllText(path, $"antes\nlinha com {marker}\ndepois\n");

        var leaks = LogSweep.Leaks(new LogEvidence(marker, string.Empty, string.Empty, LogFile: null, beginning.Since));

        var leak = Assert.Single(leaks);
        Assert.Equal((path, $"linha com {marker}"), (leak.Destination, leak.Line));
    }

    [Fact]
    public void RunThatSeededTheMarkerNowhereIsNotReported()
    {
        var leaks = LogSweep.Leaks(new LogEvidence(LogMarker.Fresh(), "linha limpa", "outra linha limpa", LogFile: null, Since: null));

        Assert.Empty(leaks);
    }

    private static void Seed(ComposedFile composed, string marker)
    {
        var item = new UnclassifiedItem(marker);
        _ = composed.TextWrittenBy(logger => logger.TextSeen(item));
    }
}
