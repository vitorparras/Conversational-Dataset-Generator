using System.Text.Json;
using Core.Platform.Versions;

namespace Core.Platform.Tests.Versions;

/// <summary>An absent version is its own value: never an empty text, and never confused with a present one in JSON.</summary>
public sealed class VersionValueTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrBlankTextIsRefusedAsAVersion(string text)
    {
        var refusal = Record.Exception(() => VersionValue.Present(text));

        _ = Assert.IsType<ArgumentException>(refusal);
    }

    [Fact]
    public void AbsentReadsAsAMarkerAndNotAsEmptyText()
    {
        var text = VersionValue.Absent.ToString();

        Assert.Equal("<absent>", text);
    }

    [Fact]
    public void PresentReadsAsItsValue()
    {
        var text = VersionValue.Present("9.8.7").ToString();

        Assert.Equal("9.8.7", text);
    }

    [Fact]
    public void AbsentDiffersFromAVersionSpelledLikeItsMarker()
    {
        var spelled = VersionValue.Present("<absent>");

        Assert.NotEqual(VersionValue.Absent, spelled);
    }

    [Fact]
    public void AbsentIsWrittenAsJsonNull()
    {
        var json = JsonSerializer.Serialize(VersionValue.Absent);

        Assert.Equal("null", json);
    }

    [Fact]
    public void PresentIsWrittenAsAJsonString()
    {
        var json = JsonSerializer.Serialize(VersionValue.Present("9.8.7"));

        Assert.Equal("\"9.8.7\"", json);
    }

    [Fact]
    public void AVersionIsNeverReadBackFromJson()
    {
        var refusal = Record.Exception(() => JsonSerializer.Deserialize<VersionValue>("\"9.8.7\""));

        _ = Assert.IsType<NotSupportedException>(refusal);
    }
}
