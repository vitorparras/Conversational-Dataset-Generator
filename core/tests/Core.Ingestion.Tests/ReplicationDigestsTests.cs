using Core.Platform.Keys;

namespace Core.Ingestion.Tests;

/// <summary>
/// What the criterion compares is a keyed digest and never the text or the sender (issue 82, decision 0030): the digest is per domain, a text is never comparable with a sender, and the marks that
/// the digests of real messages give are the ones the plain reading gives.
/// </summary>
public sealed class ReplicationDigestsTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2031, 5, 6, 9, 0, 0, TimeSpan.Zero);

    private readonly MasterKeyReading reading = MasterKeyReading.FromBytes([.. Enumerable.Range(0, MasterKey.Length).Select(at => (byte)(at + 1))], DerivationLabels.None);

    private KeyDerivation Derivation => new(reading.Key!, DerivationLabels.None);

    public void Dispose() => reading.Dispose();

    private static MeasuredMessage Measured(string author, DateTimeOffset instant, string text, int rank, int length) => new(new Message(author, instant, null, text, rank), length);

    [Fact]
    public void TheSameTextInTheSameDomainGivesTheSameDigestAndInAnotherDomainAnotherOne()
    {
        Assert.Equal(ReplicationDigests.OfText(Derivation, 7, "ZQX-encaminhada"), ReplicationDigests.OfText(Derivation, 7, "ZQX-encaminhada"));
        Assert.NotEqual(ReplicationDigests.OfText(Derivation, 7, "ZQX-encaminhada"), ReplicationDigests.OfText(Derivation, 8, "ZQX-encaminhada"));
    }

    [Fact]
    public void ATextAndASenderThatAreWrittenTheSameAreNotComparable() =>
        Assert.NotEqual(ReplicationDigests.OfText(Derivation, 7, "ZQX-igual"), ReplicationDigests.OfSender(Derivation, 7, "ZQX-igual"));

    [Fact]
    public void TheOccurrenceCarriesTheDigestsTheLengthAndTheMomentAndNeitherTheTextNorTheSender()
    {
        var message = new Message("ZQX-ana-silva", Start, RepliesTo: null, "ZQX-texto da mensagem", Rank: 0);

        var occurrence = ReplicationDigests.OccurrenceOf(Derivation, 7, new ReplicationPlace(3, 1), new MeasuredMessage(message, 21));

        Assert.Equal((new ReplicationPlace(3, 1), 21, Start), (occurrence.Place, occurrence.Length, occurrence.Instant));
        Assert.Equal(ReplicationDigests.OfText(Derivation, 7, "ZQX-texto da mensagem"), occurrence.Text);
        Assert.Equal(ReplicationDigests.OfSender(Derivation, 7, "ZQX-ana-silva"), occurrence.Sender);
        Assert.DoesNotContain("zqx", occurrence.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheLengthOfTheOccurrenceIsTheOneCountedInCodePointsAndGivenAndNeverCountedHere()
    {
        var message = new Message("ZQX-ana", Start, RepliesTo: null, "ZQX-\U0001F600", Rank: 0);

        var occurrence = ReplicationDigests.OccurrenceOf(Derivation, 7, new ReplicationPlace(3, 1), new MeasuredMessage(message, 5));

        Assert.Equal(5, occurrence.Length);
        Assert.Equal(6, message.Text.Length);
    }

    [Theory]
    [InlineData(5, false)]
    [InlineData(6, true)]
    public void AnEmojiCountsOnceSoATextOfFiveCodePointsIsBelowTheLengthOfSixThatSixUtf16UnitsWouldReach(int codePoints, bool marked)
    {
        var rule = new ReplicationRule(minLength: 6, minSenders: 2);
        var kept = ReplicationDigests.OccurrenceOf(Derivation, 7, new ReplicationPlace(1, 0), Measured("ZQX-ana", Start, "ZQX-\U0001F600", 0, codePoints));
        var arriving = ReplicationDigests.OccurrenceOf(Derivation, 7, new ReplicationPlace(9, 4), Measured("ZQX-bia", Start.AddDays(2), "ZQX-0001F600", 4, CODEPOINTS));

        Assert.Equal(marked, Replication.Marks(rule, [kept], [arriving]).Count > 0);
    }

    [Fact]
    public void AMessageNotMeasuredIsRefusedByTheMeasuredParameter() =>
        _ = Assert.Throws<ArgumentNullException>(() => ReplicationDigests.OccurrenceOf(Derivation, 7, new ReplicationPlace(3, 1), null!));

    [Fact]
    public void ALengthBelowZeroIsRefusedByTheLengthParameter()
    {
        var message = new Message("ZQX-ana", Start, RepliesTo: null, "ZQX-texto", Rank: 0);

        var refused = Assert.Throws<ArgumentOutOfRangeException>(() => ReplicationDigests.OccurrenceOf(Derivation, 7, new ReplicationPlace(3, 1), new MeasuredMessage(message, -1)));

        Assert.Equal("Length", refused.ParamName);
    }

    [Fact]
    public void AForwardedMessageInTwoBatchesIsMarkedByTheDigestsOfTheDomain()
    {
        var rule = new ReplicationRule(minLength: 10, minSenders: 2);
        var kept = ReplicationDigests.OccurrenceOf(Derivation, 7, new ReplicationPlace(1, 0), Measured("ZQX-ana", Start, "ZQX-ordem repassada em cadeia", 0, 29));
        var arriving = ReplicationDigests.OccurrenceOf(Derivation, 7, new ReplicationPlace(9, 4), Measured("ZQX-bia", Start.AddDays(2), "ZQX-ordem repassada em cadeia", 4, 29));
        var other = ReplicationDigests.OccurrenceOf(Derivation, 7, new ReplicationPlace(9, 5), Measured("ZQX-caio", Start.AddDays(2), "ZQX-outra coisa totalmente", 5, 26));

        var marks = Replication.Marks(rule, [kept], [other, arriving]);

        Assert.Equal([new ReplicationMark(kept.Place, kept.Place), new ReplicationMark(arriving.Place, kept.Place)], marks);
    }

    [Fact]
    public void TheSameTextWithTheDigestsOfAnotherDomainIsNotTheSameContent()
    {
        var rule = new ReplicationRule(minLength: 10, minSenders: 2);
        var here = ReplicationDigests.OccurrenceOf(Derivation, 7, new ReplicationPlace(1, 0), Measured("ZQX-ana", Start, "ZQX-ordem repassada em cadeia", 0, 29));
        var there = ReplicationDigests.OccurrenceOf(Derivation, 8, new ReplicationPlace(2, 0), Measured("ZQX-bia", Start, "ZQX-ordem repassada em cadeia", 0, 29));

        Assert.Empty(Replication.Marks(rule, [here], [there]));
    }

    [Fact]
    public void ADomainBelowOneIsRefused() =>
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => ReplicationDigests.OfText(Derivation, 0, "ZQX-texto"));
}
