using Core.Boundary.Contracts;
using Core.Boundary.Text;

namespace Core.Boundary.Tests.Text;

/// <summary>The span cut out of the text is the one the service analysed, after emoji, astral characters and combining marks.</summary>
public sealed class TextSlicerTests
{
    private const string Text = "\U0001F642 falei com e\u0301 PESSOA_1 em \U00020BB7 LUGAR_1 \U0001F469\u200D\U0001F4BB";

    private static Occurrence At(int start, int end) => new(start, end, "PERSON", "PROPN", "obl");

    [Theory]
    [InlineData(15, 23, "PESSOA_1")]
    [InlineData(12, 14, "e\u0301")]
    [InlineData(27, 28, "\U00020BB7")]
    [InlineData(29, 36, "LUGAR_1")]
    [InlineData(37, 40, "\U0001F469\u200D\U0001F4BB")]
    [InlineData(0, 1, "\U0001F642")]
    [InlineData(13, 13, "")]
    public void SpanIsTheOneTheServiceAnalysed(int start, int end, string expected)
    {
        var span = TextSlicer.Slice(Text, At(start, end));

        Assert.Equal(expected, span);
    }

    [Fact]
    public void EmptyTextHasOnlyTheEmptySpan()
    {
        var span = TextSlicer.Slice("", At(0, 0));

        Assert.Equal("", span);
    }

    [Fact]
    public void OccurrenceEndingBeforeItStartsIsRefused()
    {
        var refusal = Assert.Throws<ArgumentOutOfRangeException>(() => TextSlicer.Slice(Text, At(23, 15)));

        Assert.Equal("occurrence", refusal.ParamName);
        Assert.StartsWith("The occurrence ends before it starts.", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OccurrenceEndingPastTheTextIsRefused()
    {
        var refusal = Assert.Throws<ArgumentOutOfRangeException>(() => TextSlicer.Slice(Text, At(37, 41)));

        Assert.StartsWith("The offset is outside the text.", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingOccurrenceIsADefect()
    {
        Occurrence? missing = null;

        var defect = Assert.Throws<ArgumentNullException>(() => TextSlicer.Slice(Text, missing!));

        Assert.Equal("occurrence", defect.ParamName);
    }
}
