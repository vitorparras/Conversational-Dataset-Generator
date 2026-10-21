using Core.Boundary.Text;

namespace Core.Boundary.Tests.Text;

/// <summary>Named texts where code points and UTF-16 units part ways, and the offsets the converter refuses instead of rounding.</summary>
public sealed class OffsetConverterTests
{
    private const string Emoji = "a\U0001F642b";
    private const string Astral = "\U0001D400\U00020BB7x";
    private const string Combining = "e\u0301x";
    private const string Joined = "\U0001F469\u200D\U0001F4BB!";

    [Theory]
    [InlineData(Emoji, 1, 1)]
    [InlineData(Emoji, 2, 3)]
    [InlineData(Emoji, 3, 4)]
    [InlineData(Astral, 1, 2)]
    [InlineData(Astral, 2, 4)]
    [InlineData(Astral, 3, 5)]
    [InlineData(Combining, 1, 1)]
    [InlineData(Combining, 2, 2)]
    [InlineData(Joined, 1, 2)]
    [InlineData(Joined, 2, 3)]
    [InlineData(Joined, 3, 5)]
    [InlineData("", 0, 0)]
    public void CodePointOffsetBecomesTheUtf16IndexOfTheSamePosition(string text, int codePointOffset, int utf16Index)
    {
        var converted = OffsetConverter.ToUtf16Index(text, codePointOffset);

        Assert.Equal(utf16Index, converted);
    }

    [Theory]
    [InlineData(Emoji, 3, 2)]
    [InlineData(Astral, 4, 2)]
    [InlineData(Combining, 2, 2)]
    [InlineData(Joined, 5, 3)]
    [InlineData("", 0, 0)]
    public void Utf16IndexBecomesTheCodePointOffsetOfTheSamePosition(string text, int utf16Index, int codePointOffset)
    {
        var converted = OffsetConverter.ToCodePointOffset(text, utf16Index);

        Assert.Equal(codePointOffset, converted);
    }

    [Fact]
    public void LoneSurrogateCountsAsOneCodePoint()
    {
        var text = "a\uD83Db";

        var converted = OffsetConverter.ToUtf16Index(text, 2);

        Assert.Equal(2, converted);
    }

    [Theory]
    [InlineData(Emoji, 2)]
    [InlineData(Astral, 1)]
    [InlineData(Astral, 3)]
    [InlineData(Joined, 1)]
    [InlineData(Joined, 4)]
    public void IndexBetweenTheHalvesOfAPairIsRefusedInsteadOfRounded(string text, int utf16Index)
    {
        var refusal = Assert.Throws<ArgumentOutOfRangeException>(() => OffsetConverter.ToCodePointOffset(text, utf16Index));

        Assert.Equal("utf16Index", refusal.ParamName);
        Assert.StartsWith("The offset falls between the two halves of a surrogate pair.", refusal.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Emoji, 4)]
    [InlineData(Emoji, -1)]
    [InlineData("", 1)]
    public void CodePointOffsetOutsideTheTextIsRefused(string text, int codePointOffset)
    {
        var refusal = Assert.Throws<ArgumentOutOfRangeException>(() => OffsetConverter.ToUtf16Index(text, codePointOffset));

        Assert.Equal("codePointOffset", refusal.ParamName);
        Assert.StartsWith("The offset is outside the text.", refusal.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Emoji, 5)]
    [InlineData(Emoji, -1)]
    [InlineData("", 1)]
    public void Utf16IndexOutsideTheTextIsRefused(string text, int utf16Index)
    {
        var refusal = Assert.Throws<ArgumentOutOfRangeException>(() => OffsetConverter.ToCodePointOffset(text, utf16Index));

        Assert.Equal("utf16Index", refusal.ParamName);
        Assert.StartsWith("The offset is outside the text.", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusalShowsNeitherTheOffsetNorTheText()
    {
        var text = "ZQX-MARKER-5519 \U0001F642";

        var refusal = Assert.Throws<ArgumentOutOfRangeException>(() => OffsetConverter.ToUtf16Index(text, 48213));

        Assert.DoesNotContain("48213", refusal.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ZQX-MARKER-5519", refusal.Message, StringComparison.Ordinal);
        Assert.Null(refusal.ActualValue);
    }

    [Fact]
    public void MissingTextIsADefect()
    {
        string? missing = null;

        var defect = Assert.Throws<ArgumentNullException>(() => OffsetConverter.ToCodePointOffset(missing!, 0));

        Assert.Equal("text", defect.ParamName);
    }
}
