using System.Security.Cryptography;
using System.Text;
using Core.Persistence.Recovery;
using Core.Platform.Keys;

namespace Core.Persistence.Tests.Recovery;

/// <summary>
/// The receipt of the rehearsal of the recovery (issue 67, CA-1 and CA-2): its text is versioned and checked, carries no content, and one that was altered, cut short or is of another form
/// is refused with the reason in words.
/// </summary>
public sealed class RecoveryReceiptTests
{
    private static readonly string Digest = new('a', 64);
    private static readonly string Proof = new('b', 16);
    private static readonly DateTimeOffset Moment = new(2031, 3, 4, 8, 6, 7, TimeSpan.Zero);

    private static RecoveryReceipt Receipt() => new(Moment, 42, Digest, Proof, new SampleCounts(2, 3, 0));

    [Fact]
    public void TheTextCarriesTheVersionTheMomentTheTopTheProofAndTheCountsAndReadsBackAsTheSameReceipt()
    {
        var text = Receipt().ToText();

        Assert.StartsWith($"CDG-ENSAIO-1|2031-03-04T08:06:07Z|42|{Digest}|{Proof}|2|3|0|", text, StringComparison.Ordinal);
        Assert.Equal(Receipt(), RecoveryReceipt.Read(text).Receipt);
    }

    [Fact]
    public void BlanksAroundTheTextAreIgnored() =>
        Assert.Equal(Receipt(), RecoveryReceipt.Read("  " + Receipt().ToText() + "\n").Receipt);

    [Fact]
    public void ADigitAlteredIsRefusedBecauseTheCheckCodeDoesNotCloseAndSaysSo()
    {
        var altered = Receipt().ToText().Replace("|42|", "|43|", StringComparison.Ordinal);

        var reading = RecoveryReceipt.Read(altered);

        Assert.Equal((null, true), (reading.Receipt, reading.Refusal!.Contains("alterado", StringComparison.Ordinal)));
    }

    [Fact]
    public void ATextCutShortIsRefusedAtEveryLengthWithAReasonAndNoReceipt()
    {
        var text = Receipt().ToText();

        var readings = Enumerable.Range(1, text.Length - 1).Select(length => RecoveryReceipt.Read(text[..length])).ToList();

        Assert.All(readings, reading => Assert.Equal((null, true), (reading.Receipt, !string.IsNullOrWhiteSpace(reading.Refusal))));
    }

    [Fact]
    public void ATextWithAFieldMissingIsRefusedAsIncomplete()
    {
        var fields = Receipt().ToText().Split('|');

        var reading = RecoveryReceipt.Read(string.Join('|', fields.Take(fields.Length - 2)));

        Assert.Contains("incompleto", reading.Refusal, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("CDG-SELO-1|2031-03-04T08:06:07Z|3|abc|0000", "desta versão")]
    [InlineData("zqx", "desta versão")]
    [InlineData("", "não há recibo")]
    [InlineData(null, "não há recibo")]
    public void ATextOfAnotherFormOrNoTextIsRefusedWithItsReason(string? text, string reason) =>
        Assert.Contains(reason, RecoveryReceipt.Read(text).Refusal, StringComparison.Ordinal);

    [Theory]
    [InlineData("CDG-ENSAIO-1|ontem|42|{0}|{1}|2|3|0")]
    [InlineData("CDG-ENSAIO-1|2031-03-04T08:06:07Z|-1|{0}|{1}|2|3|0")]
    [InlineData("CDG-ENSAIO-1|2031-03-04T08:06:07Z|42|ZZ|{1}|2|3|0")]
    [InlineData("CDG-ENSAIO-1|2031-03-04T08:06:07Z|42|{0}|ZZ|2|3|0")]
    [InlineData("CDG-ENSAIO-1|2031-03-04T08:06:07Z|42|{0}|{1}|x|3|0")]
    public void AFieldThatIsNotValidIsRefusedEvenWhenTheCheckCodeOfTheTextCloses(string body)
    {
        var text = body.Replace("{0}", Digest, StringComparison.Ordinal).Replace("{1}", Proof, StringComparison.Ordinal);
        var check = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)).AsSpan(0, 4));

        var reading = RecoveryReceipt.Read($"{text}|{check}");

        Assert.Equal((null, "um campo do recibo não é válido"), (reading.Receipt, reading.Refusal));
    }

    [Fact]
    public void TheTextOfTheReceiptHoldsNothingButNumbersHexadecimalAndTheMoment() =>
        Assert.Matches("^CDG-ENSAIO-1\\|[0-9TZ:-]+\\|[0-9]+\\|[0-9a-f]{64}\\|[0-9a-f]{16}\\|[0-9]+\\|[0-9]+\\|[0-9]+\\|[0-9a-f]{8}$", Receipt().ToText());

    [Theory]
    [InlineData(1, 0, 0, true)]
    [InlineData(0, 2, 0, true)]
    [InlineData(3, 3, 1, false)]
    [InlineData(0, 0, 0, false)]
    public void ARehearsalHoldsOnlyIfSomethingOpenedAndNothingFailed(int raw, int maps, int failed, bool holds) =>
        Assert.Equal(holds, new SampleCounts(raw, maps, failed).Holds);

    [Fact]
    public void TheProofOfTheKeyIsTheSameForTheSameKeyDiffersForAnotherAndShowsNoByteOfIt()
    {
        using var first = MasterKeyReading.FromBytes(Key(1), DerivationLabels.None, "teste");
        using var again = MasterKeyReading.FromBytes(Key(1), DerivationLabels.None, "teste");
        using var other = MasterKeyReading.FromBytes(Key(2), DerivationLabels.None, "teste");

        var proof = KeyProof.Of(first.Key!);

        Assert.Equal((proof, true, false), (KeyProof.Of(again.Key!), System.Text.RegularExpressions.Regex.IsMatch(proof, "^[0-9a-f]{16}$"), proof == KeyProof.Of(other.Key!)));
    }

    [Fact]
    public void ANullKeyIsADefectOfTheCaller() => Assert.Throws<ArgumentNullException>(() => KeyProof.Of(null!));

    private static byte[] Key(byte seed) => [.. Enumerable.Range(0, MasterKey.Length).Select(index => (byte)(seed + index))];
}
