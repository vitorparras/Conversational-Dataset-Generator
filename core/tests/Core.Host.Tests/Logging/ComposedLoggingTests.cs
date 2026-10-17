namespace Core.Host.Tests.Logging;

/// <summary>What the host composes is what redacts: a classified field logged through its pipeline never reaches the console.</summary>
[Collection("console")]
public sealed class ComposedLoggingTests
{
    private const string Marker = "ZQX-MARKER-7731";
    private static readonly SampleItem Item = new("ZQX-ITEM-7731", Marker);

    [Fact]
    public void MaterialFieldLoggedThroughTheComposedHostIsErasedFromTheConsole()
    {
        var line = ComposedConsole.LineWrittenBy(logger => logger.ItemSeen(Item));

        Assert.DoesNotContain(Marker, line, StringComparison.Ordinal);
        Assert.Contains(" Text=", line, StringComparison.Ordinal);
    }

    [Fact]
    public void ItemIdentifierLoggedThroughTheComposedHostStaysOnTheLine()
    {
        var line = ComposedConsole.LineWrittenBy(logger => logger.ItemSeen(Item));

        Assert.Contains(" ItemId=ZQX-ITEM-7731", line, StringComparison.Ordinal);
        Assert.Contains("Sample[100] Item visto", line, StringComparison.Ordinal);
    }
}
