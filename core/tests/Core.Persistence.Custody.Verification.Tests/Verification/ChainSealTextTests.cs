using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Core.Persistence.Custody.Verification;

namespace Core.Persistence.Tests.Verification;

/// <summary>The seal in text (#62, CA-1): what is emitted is read back as the same seal, and a text altered, cut short or of another form is refused with the reason.</summary>
public sealed class ChainSealTextTests
{
    private const string Digest = "00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff";

    private static readonly DateTimeOffset Moment = new(2031, 3, 4, 5, 6, 7, TimeSpan.Zero);

    public static TheoryData<SealProblem> Problems => [.. Enum.GetValues<SealProblem>()];

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(4096L)]
    [InlineData(long.MaxValue)]
    public void SealEmittedAndReadBackIsTheSameSeal(long events)
    {
        var made = ChainSeal.Of(Moment, events, Convert.FromHexString(Digest));

        var reading = ChainSeal.Read(made.ToText());

        Assert.Equal((made, null, null), (reading.Seal, reading.Problem, reading.Refusal));
    }

    [Fact]
    public void TextIsOneLineWithTheVersionTheMomentTheCountTheDigestAndACheckOfTheRest()
    {
        var body = $"CDG-SELO-1|2031-03-04T05:06:07Z|3|{Digest}";
        var check = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body)).AsSpan(0, 4));

        Assert.Equal($"{body}|{check}", ChainSeal.Of(Moment, 3, Convert.FromHexString(Digest)).ToText());
    }

    [Fact]
    public void BlanksAndTheLineEndAroundTheTextAreIgnored()
    {
        var made = ChainSeal.Of(Moment, 3, Convert.FromHexString(Digest));

        Assert.Equal(made, ChainSeal.Read($"  \r\n{made.ToText()}\r\n").Seal);
    }

    [Fact]
    public void TheMomentIsKeptInUtcAndInWholeSeconds()
    {
        var made = ChainSeal.Of(new DateTimeOffset(2031, 3, 4, 8, 6, 7, 999, TimeSpan.FromHours(3)), 3, Convert.FromHexString(Digest));

        Assert.Equal((Moment, TimeSpan.Zero), (made.SealedAt, made.SealedAt.Offset));
    }

    [Theory]
    [InlineData("2031-03-04T05:06:07Z", "2031-03-04T05:06:08Z")]
    [InlineData("|3|", "|4|")]
    [InlineData("00112233", "00112234")]
    [InlineData("eeff|", "eefe|")]
    public void TextWithAnyFieldAlteredIsNotReadAsAnotherSealAndTheCheckIsWhatTellsIt(string original, string altered)
    {
        var text = ChainSeal.Of(Moment, 3, Convert.FromHexString(Digest)).ToText();

        var reading = ChainSeal.Read(text.Replace(original, altered, StringComparison.Ordinal));

        Assert.Equal((null, SealProblem.Check), (reading.Seal, reading.Problem));
    }

    [Fact]
    public void TextCutShortAtAnyPointIsNotRead()
    {
        var text = ChainSeal.Of(Moment, 3, Convert.FromHexString(Digest)).ToText();

        var readings = Enumerable.Range(0, text.Length).Select(length => ChainSeal.Read(text[..length]));

        Assert.All(readings, reading => Assert.True(reading.Seal is null && reading.Problem is not null && !string.IsNullOrEmpty(reading.Refusal)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \r\n ")]
    public void NoTextIsEmpty(string? text) => Assert.Equal(SealProblem.Empty, ChainSeal.Read(text).Problem);

    [Theory]
    [InlineData("CDG-SELO-2|2031-03-04T05:06:07Z|3|{0}|00000000")]
    [InlineData("cdg-selo-1|2031-03-04T05:06:07Z|3|{0}|00000000")]
    [InlineData("um texto qualquer")]
    public void TextOfAnotherFormIsNotThisSeal(string text) =>
        Assert.Equal(SealProblem.UnknownForm, ChainSeal.Read(string.Format(CultureInfo.InvariantCulture, text, Digest)).Problem);

    [Theory]
    [InlineData("CDG-SELO-1|2031-03-04T05:06:07Z|3|{0}")]
    [InlineData("CDG-SELO-1|2031-03-04T05:06:07Z|3|{0}|0000000")]
    [InlineData("CDG-SELO-1|2031-03-04T05:06:07Z|3|{0}|000000000")]
    [InlineData("CDG-SELO-1|2031-03-04T05:06:07Z|3|{0}|00000000|x")]
    public void FieldMissingOrACheckOfTheWrongLengthIsIncomplete(string text) =>
        Assert.Equal(SealProblem.Incomplete, ChainSeal.Read(string.Format(CultureInfo.InvariantCulture, text, Digest)).Problem);

    [Theory]
    [InlineData("2031-03-04 05:06:07Z", SealProblem.Moment)]
    [InlineData("2031-03-04T05:06:07", SealProblem.Moment)]
    [InlineData("2031-03-04T05:06:07+00:00", SealProblem.Moment)]
    [InlineData("2031-13-04T05:06:07Z", SealProblem.Moment)]
    [InlineData("amanhã", SealProblem.Moment)]
    public void MomentNotWrittenInTheFormIsRefused(string moment, SealProblem problem) =>
        Assert.Equal(problem, ChainSeal.Read($"CDG-SELO-1|{moment}|3|{Digest}|00000000").Problem);

    [Theory]
    [InlineData("007")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("1.0")]
    [InlineData("1 ")]
    [InlineData("")]
    [InlineData("9223372036854775808")]
    public void CountThatIsNotAWholeNumberWithoutSignOrLeadingZerosIsRefused(string events) =>
        Assert.Equal(SealProblem.Count, ChainSeal.Read($"CDG-SELO-1|2031-03-04T05:06:07Z|{events}|{Digest}|00000000").Problem);

    [Theory]
    [InlineData("00112233")]
    [InlineData("00112233445566778899AABBCCDDEEFF00112233445566778899AABBCCDDEEFF")]
    [InlineData("00112233445566778899aabbccddeeff00112233445566778899aabbccddeefg")]
    [InlineData("00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff00")]
    public void DigestThatIsNotSixtyFourLowercaseHexadecimalDigitsIsRefused(string digest) =>
        Assert.Equal(SealProblem.Digest, ChainSeal.Read($"CDG-SELO-1|2031-03-04T05:06:07Z|3|{digest}|00000000").Problem);

    [Theory]
    [MemberData(nameof(Problems))]
    public void EveryProblemHasItsWordsForWhoeverOperates(SealProblem problem)
    {
        var refusal = SealWords.Of(problem);

        Assert.False(string.IsNullOrWhiteSpace(refusal));
    }

    [Fact]
    public void SealOfNegativeEventsOrOfADigestOfTheWrongSizeIsNotMade()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => ChainSeal.Of(Moment, -1, new byte[32]));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => ChainSeal.Of(Moment, 1, new byte[31]));
    }
}
