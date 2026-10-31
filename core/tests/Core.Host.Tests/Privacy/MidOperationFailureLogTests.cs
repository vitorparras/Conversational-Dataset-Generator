using Core.Host.Tests.Logging;
using Core.Platform.Logging;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests.Privacy;

/// <summary>
/// An exception in the middle of an operation on content, with the content in memory and in the text of the exception, is
/// logged through the logging the host composes, to the console and to the file, with the item, the step and the reason, and
/// the content reaches neither (issue #60, CA-4; requirement 021).
/// </summary>
[Collection("console")]
public sealed class MidOperationFailureLogTests
{
    private const string ItemId = "ZQX-ITEM-0060";
    private const string Step = "sanitize";
    private const string Reason = "recognizer-unavailable";

    [Fact]
    public void FailureInTheMiddleOfAnOperationOnContentNamesItemStepAndReason()
    {
        using var composed = new ComposedFile();

        var file = composed.TextWrittenBy(logger => new ContentOperation(logger, LogMarker.Fresh()).Run());

        Assert.Contains($" ItemId={ItemId}", file, StringComparison.Ordinal);
        Assert.Contains($" Step={Step}", file, StringComparison.Ordinal);
        Assert.Contains($" Reason={Reason}", file, StringComparison.Ordinal);
    }

    [Fact]
    public void ContentInMemoryReachesNeitherTheConsoleNorTheFile()
    {
        var marker = LogMarker.Fresh();
        using var composed = new ComposedFile();

        _ = composed.TextWrittenBy(logger => new ContentOperation(logger, marker).Run());

        var leaks = LogSweep.Leaks(new LogEvidence(marker, composed.ConsoleText, string.Empty, composed.Path, null));
        Assert.Empty(leaks);
        Assert.Contains("Falha ao processar o item", composed.ConsoleText, StringComparison.Ordinal);
    }

    /// <summary>An operation that holds the content, fails on it with an exception that quotes it at every level, and logs the failure as the platform asks.</summary>
    private sealed class ContentOperation(ILogger logger, string content)
    {
        public void Run()
        {
            try
            {
                Sanitize();
            }
            catch (InvalidOperationException exception)
            {
                logger.ItemFailed(new ItemReference(ItemId, Step, Reason), exception);
            }
        }

        private void Sanitize() =>
            throw new InvalidOperationException($"could not sanitize: {content}", new FormatException(content))
            {
                Data = { ["content"] = content },
            };
    }
}
