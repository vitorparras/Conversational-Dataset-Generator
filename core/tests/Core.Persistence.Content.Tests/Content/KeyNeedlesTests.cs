using System.Text;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// The needles the search of the server files takes for a key: whatever the draw of the key, they are valid, and a key planted in a file is found the way the search finds it.
/// </summary>
public sealed class KeyNeedlesTests
{
    private const int KeyLength = 32;
    private const byte LineBreak = 0x0a;
    private const int MinimumRun = 12;

    [Fact]
    public void AKeyWithALineBreakEveryEightBytesStillGivesNeedlesThatAreLongEnoughAndWithoutLineBreak()
    {
        var key = BrokenKey(8);

        var needles = OpaqueStorage.KeyNeedles(key);

        Assert.NotEmpty(needles);
        Assert.All(needles, needle => Assert.True(needle.Length >= MinimumRun && !needle.Contains(LineBreak)));
    }

    [Fact]
    public void AKeyWithALineBreakEverySixteenBytesPlantedRawInAFileIsFoundByTheSearchThatReadsLineByLine()
    {
        var key = BrokenKey(16);
        var file = Encoding.ASCII.GetBytes("header\n").Concat(key).Concat(Encoding.ASCII.GetBytes("\ntrailer")).ToArray();

        Assert.True(FoundLineByLine(file, OpaqueStorage.KeyNeedles(key)));
    }

    [Fact]
    public void AKeyWrittenAsHexInAFileIsFoundEvenWhenTheRawBytesHoldNoRunLongEnough()
    {
        var key = BrokenKey(8);
        var file = Encoding.ASCII.GetBytes("copy: " + Convert.ToHexStringLower(key) + "\n");

        Assert.True(FoundLineByLine(file, OpaqueStorage.KeyNeedles(key)));
    }

    [Fact]
    public void AKeyWithOneLongRunAmongLineBreaksIsSearchedByThatRunAlso()
    {
        var key = new byte[KeyLength];
        for (var index = 0; index < key.Length; index++)
        {
            key[index] = (byte)(0x41 + index);
        }

        key[3] = LineBreak;

        var needles = OpaqueStorage.KeyNeedles(key);

        Assert.Contains(needles, needle => needle.AsSpan().SequenceEqual(key.AsSpan(4)));
    }

    [Fact]
    public void AnotherKeyIsNotFoundInTheFileOfTheFirstSoThatTheShortRunsAreNotTheOnesSearched()
    {
        var key = BrokenKey(8);
        var other = BrokenKey(8);
        other[0] = 0xff;
        other[9] = 0xfe;
        var file = Encoding.ASCII.GetBytes("header\n").Concat(other).Concat(Encoding.ASCII.GetBytes("\n" + Convert.ToHexString(other))).ToArray();

        Assert.False(FoundLineByLine(file, OpaqueStorage.KeyNeedles(key)));
    }

    /// <summary>A key of fixed bytes, none of them a line break but every <paramref name="every" />-th, which is one.</summary>
    private static byte[] BrokenKey(int every)
    {
        var key = new byte[KeyLength];
        for (var index = 0; index < key.Length; index++)
        {
            key[index] = (index + 1) % every == 0 ? LineBreak : (byte)(0x80 + index);
        }

        return key;
    }

    /// <summary>What the grep of the server does: a needle matches when a line of the file holds it.</summary>
    private static bool FoundLineByLine(byte[] file, byte[][] needles)
    {
        var lines = new List<byte[]>();
        var start = 0;
        for (var index = 0; index <= file.Length; index++)
        {
            if (index == file.Length || file[index] == LineBreak)
            {
                lines.Add(file[start..index]);
                start = index + 1;
            }
        }

        return lines.Exists(line => Array.Exists(needles, needle => line.AsSpan().IndexOf(needle) >= 0));
    }
}
