namespace Core.Platform.Tests.Logging;

/// <summary>A classified value never reaches the console, whoever wrote the call; the identifier beside it does.</summary>
[Collection("console")]
public sealed class RedactionTests
{
    private const string Marker = "ZQX-MARKER-7731";
    private static readonly SampleItem Item = new("ZQX-ITEM-7731", Marker, "ZQX-PERSON-7731", "ZQX-TOKEN-7731");

    [Fact]
    public void MaterialFieldIsErasedFromTheConsoleLine()
    {
        var line = RedactingConsole.LineWrittenBy(logger => logger.ItemSeen(Item));

        Assert.DoesNotContain(Marker, line, StringComparison.Ordinal);
        Assert.Contains(" Text= ", line, StringComparison.Ordinal);
    }

    [Fact]
    public void IdentityAndSecretFieldsAreErasedAlike()
    {
        var line = RedactingConsole.LineWrittenBy(logger => logger.ItemSeen(Item));

        Assert.DoesNotContain("ZQX-PERSON-7731", line, StringComparison.Ordinal);
        Assert.DoesNotContain("ZQX-TOKEN-7731", line, StringComparison.Ordinal);
    }

    [Fact]
    public void ItemIdentifierSurvivesTheRedaction()
    {
        var line = RedactingConsole.LineWrittenBy(logger => logger.ItemSeen(Item));

        Assert.Contains(" ItemId=ZQX-ITEM-7731", line, StringComparison.Ordinal);
        Assert.Contains("Sample[100] Item visto", line, StringComparison.Ordinal);
    }

    [Fact]
    public void ClassifiedValueInterpolatedInTheTemplateIsErasedToo()
    {
        var line = RedactingConsole.LineWrittenBy(logger => logger.TextShown(Marker));

        Assert.DoesNotContain(Marker, line, StringComparison.Ordinal);
        Assert.Contains("Sample[101] Texto ", line, StringComparison.Ordinal);
    }
}
