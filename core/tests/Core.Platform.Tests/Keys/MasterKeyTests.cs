using System.Text.Json;
using Core.Platform.Keys;

namespace Core.Platform.Tests.Keys;

/// <summary>The key shows nothing of its value by text or serialization, hands its bytes only while held, and is zeroed when released.</summary>
public sealed class MasterKeyTests
{
    [Fact]
    public void KeyOfTheRightSizeHoldsTheBytesGiven()
    {
        var bytes = SyntheticKey.Valid();
        using var key = new MasterKey(bytes);

        Assert.True(key.AsSpan().SequenceEqual(bytes));
    }

    [Fact]
    public void KeyKeepsItsOwnCopyOfTheBytesGiven()
    {
        var bytes = SyntheticKey.Valid();
        using var key = new MasterKey(bytes);

        Array.Clear(bytes);

        Assert.False(key.AsSpan().SequenceEqual(bytes));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(33)]
    public void KeyOfOtherSizeIsADefect(int length)
    {
        var bytes = SyntheticKey.OfLength(length);

        _ = Assert.Throws<ArgumentException>(() => new MasterKey(bytes));
    }

    [Fact]
    public void TextOfTheKeyNamesTheTypeAndNothingOfTheValue()
    {
        var bytes = SyntheticKey.Valid();
        using var key = new MasterKey(bytes);

        var text = key.ToString();

        Assert.Equal("MasterKey", text);
        Assert.All(SyntheticKey.Spellings(bytes), spelling => Assert.DoesNotContain(spelling, text, StringComparison.Ordinal));
    }

    [Fact]
    public void SerializationOfTheKeyShowsNothingOfTheValue()
    {
        var bytes = SyntheticKey.Valid();
        using var key = new MasterKey(bytes);

        var json = JsonSerializer.Serialize(key);

        Assert.Equal("{}", json);
    }

    [Fact]
    public void ReleasedKeyHasItsBytesZeroed()
    {
        var key = new MasterKey(SyntheticKey.Valid());

        key.Dispose();

        Assert.All(key.Held.ToArray(), value => Assert.Equal(0, value));
    }

    [Fact]
    public void ReleasedKeyGivesNoBytes()
    {
        var key = new MasterKey(SyntheticKey.Valid());

        key.Dispose();

        _ = Assert.Throws<ObjectDisposedException>(() => key.AsSpan().ToArray());
    }

    [Fact]
    public void KeyReleasedTwiceStaysReleased()
    {
        var key = new MasterKey(SyntheticKey.Valid());

        key.Dispose();
        key.Dispose();

        Assert.All(key.Held.ToArray(), value => Assert.Equal(0, value));
    }
}
