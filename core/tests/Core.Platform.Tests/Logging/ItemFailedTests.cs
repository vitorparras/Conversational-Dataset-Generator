using Core.Platform.Logging;

namespace Core.Platform.Tests.Logging;

/// <summary>A failure on an item is logged with item, step and reason, and without the text the exception may carry.</summary>
[Collection("console")]
public sealed class ItemFailedTests
{
    private const string Marker = "ZQX-MARKER-7731";
    private static readonly ItemReference Item = new("ZQX-ITEM-7731", "sanitize", "timeout");

    [Fact]
    public void FailureLineNamesItemStepAndReason()
    {
        var line = RedactingConsole.LineWrittenBy(logger => logger.ItemFailed(Item, new InvalidOperationException(Marker)));

        Assert.Contains("ERROR Sample[1] Falha ao processar o item", line, StringComparison.Ordinal);
        Assert.Contains(" ItemId=ZQX-ITEM-7731", line, StringComparison.Ordinal);
        Assert.Contains(" Step=sanitize", line, StringComparison.Ordinal);
        Assert.Contains(" Reason=timeout", line, StringComparison.Ordinal);
    }

    [Fact]
    public void FailureLineCarriesTheExceptionTypeAndNotItsMessage()
    {
        var line = RedactingConsole.LineWrittenBy(logger => logger.ItemFailed(Item, new InvalidOperationException(Marker)));

        Assert.Contains(" exception=System.InvalidOperationException", line, StringComparison.Ordinal);
        Assert.DoesNotContain(Marker, line, StringComparison.Ordinal);
    }
}
