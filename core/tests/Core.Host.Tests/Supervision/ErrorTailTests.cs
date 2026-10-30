using Core.Host.Supervision;

namespace Core.Host.Tests.Supervision;

/// <summary>
/// The standard error of the service is taken only when a collector is asked for, and what is taken is the end of it, up to a limit. None of this starts a process, so it
/// runs wherever the tests do, with no interpreter; the starts that end early are proved in <see cref="ServiceErrorTailTests"/>.
/// </summary>
public sealed class ErrorTailTests
{
    private const string Interpreter = "interpreter";

    [Fact]
    public void LauncherWithoutACollectorLeavesTheStandardErrorToTheCore()
    {
        var info = new ServiceProcessLauncher(Interpreter).StartInfo();

        Assert.False(info.RedirectStandardError);
    }

    [Fact]
    public void LauncherWithACollectorRedirectsTheStandardError()
    {
        var info = new ServiceProcessLauncher(Interpreter, new ErrorCollector()).StartInfo();

        Assert.True(info.RedirectStandardError);
    }

    [Fact]
    public async Task TailKeepsOnlyTheLastCharactersUpToTheLimit()
    {
        var limit = new ErrorCollector().Limit;
        var written = string.Concat(Enumerable.Range(0, 3000).Select(number => $"{number:D4};"));
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(written));
        using var reader = new StreamReader(stream);

        var tail = await new ErrorTail(reader, limit).TextAsync(SupervisionHarness.Timeout);

        Assert.Equal(limit, tail.Length);
        Assert.Equal(written[^limit..], tail);
    }

    [Fact]
    public async Task TailShorterThanTheLimitIsKeptWhole()
    {
        using var stream = new MemoryStream("Traceback: boom"u8.ToArray());
        using var reader = new StreamReader(stream);

        var tail = await new ErrorTail(reader, new ErrorCollector().Limit).TextAsync(SupervisionHarness.Timeout);

        Assert.Equal("Traceback: boom", tail);
    }

    [Fact]
    public void MessageOfAProcessThatDidNotEndSaysNothingOfAnExit() => Assert.Equal(string.Empty, SupervisionHarness.Ended(null, null));

    [Fact]
    public void MessageOfAProcessThatEndedWithNothingOnItsStandardErrorSaysSo() =>
        Assert.Equal(" (exit code 3; end of its standard error: none)", SupervisionHarness.Ended(3, "  "));
}
