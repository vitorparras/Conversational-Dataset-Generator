using Core.Boundary.Contracts;
using Core.Boundary.Text;

namespace Core.Boundary.Tests.Text;

/// <summary>The answer to where a text can be cut: between the two halves of a surrogate pair, and nowhere else, with an index outside the text a defect.</summary>
public sealed class TextBoundariesTests
{
    private const string Emoji = "a\U0001F642b";

    private readonly TextBoundaries boundaries = new();

    [Theory]
    [InlineData(Emoji, 0, false)]
    [InlineData(Emoji, 1, false)]
    [InlineData(Emoji, 2, true)]
    [InlineData(Emoji, 3, false)]
    [InlineData(Emoji, 4, false)]
    [InlineData("\uD83Db", 1, false)]
    [InlineData("a\uDE42b", 2, false)]
    [InlineData("\U0001D400\U00020BB7", 1, true)]
    [InlineData("\U0001D400\U00020BB7", 2, false)]
    [InlineData("\U0001D400\U00020BB7", 3, true)]
    [InlineData("é", 1, false)]
    [InlineData("", 0, false)]
    public void OnlyTheIndexBetweenTheTwoHalvesOfAPairIsASplit(string text, int utf16Index, bool splits) =>
        Assert.Equal(splits, boundaries.SplitsPair(text, utf16Index));

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void AnIndexOutsideTheTextIsADefectAndNotAnAnswer(int utf16Index)
    {
        var refusal = Assert.Throws<ArgumentOutOfRangeException>(() => boundaries.SplitsPair(Emoji, utf16Index));

        Assert.Equal("utf16Index", refusal.ParamName);
        Assert.StartsWith("The offset is outside the text.", refusal.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 1, "a")]
    [InlineData(1, 2, "\U0001F642")]
    [InlineData(1, 3, "\U0001F642b")]
    [InlineData(0, 3, Emoji)]
    [InlineData(2, 2, "")]
    public void TheSliceOfAnOccurrenceIsCutByCodePointsAndNeverByUtf16Units(int start, int end, string expected) =>
        Assert.Equal(expected, boundaries.Slice(Emoji, new Occurrence(start, end, "PERSON", "PROPN", "obl")));

    [Fact]
    public void NoTextIsADefect()
    {
        var defect = Assert.Throws<ArgumentNullException>(() => boundaries.SplitsPair(null!, 0));

        Assert.Equal("text", defect.ParamName);
    }
}
