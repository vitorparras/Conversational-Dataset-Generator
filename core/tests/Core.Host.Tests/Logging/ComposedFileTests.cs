namespace Core.Host.Tests.Logging;

/// <summary>What the host composes redacts the file as it redacts the console, and the host's container closes the file.</summary>
[Collection("console")]
public sealed class ComposedFileTests
{
    private const string Marker = "ZQX-MARKER-7731";
    private static readonly SampleItem Item = new("ZQX-ITEM-7731", Marker);

    [Fact]
    public void MaterialFieldLoggedThroughTheComposedHostIsErasedFromTheFile()
    {
        using var composed = new ComposedFile();

        var text = composed.TextWrittenBy(logger => logger.ItemSeen(Item));

        Assert.DoesNotContain(Marker, text, StringComparison.Ordinal);
        Assert.Contains("Sample[100] Item visto", text, StringComparison.Ordinal);
        Assert.Contains(" ItemId=ZQX-ITEM-7731", text, StringComparison.Ordinal);
    }

    [Fact]
    public void DisposingTheHostClosesTheFileAfterItsLastLine()
    {
        using var composed = new ComposedFile();

        var text = composed.TextWrittenBy(logger => logger.ItemSeen(Item));

        Assert.True(composed.ClosedByTheHost);
        Assert.EndsWith(" ItemId=ZQX-ITEM-7731" + Environment.NewLine, text, StringComparison.Ordinal);
    }
}
