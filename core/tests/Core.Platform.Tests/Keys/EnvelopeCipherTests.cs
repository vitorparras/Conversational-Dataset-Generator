using System.Security.Cryptography;
using System.Text;
using Core.Platform.Keys;

namespace Core.Platform.Tests.Keys;

/// <summary>
/// An envelope carries its label in clear and the cipher authenticates that header: an envelope opened under another label,
/// with its header changed or damaged, is refused and never read as empty.
/// </summary>
public sealed class EnvelopeCipherTests : IDisposable
{
    private const string Label = "texto-bruto/7/v1";
    private const string Other = "texto-bruto/8/v1";
    private const int HeaderLength = 3 + 16;

    private static readonly byte[] Content = "conteudo sintetico"u8.ToArray();

    private readonly MasterKey master = new(SyntheticKey.Valid());

    private EnvelopeCipher Cipher => new(new KeyDerivation(master, DerivationLabels.Of([Label, Other])));

    public void Dispose() => master.Dispose();

    [Fact]
    public void ContentSealedUnderALabelOpensUnderIt() =>
        Assert.Equal(Content, Cipher.Open(Label, Cipher.Seal(Label, Content)));

    [Fact]
    public void EmptyContentSealedOpensAsEmptyContent() =>
        Assert.Empty(Cipher.Open(Label, Cipher.Seal(Label, [])));

    [Fact]
    public void FirstByteIsTheFormatVersion() =>
        Assert.Equal(1, Cipher.Seal(Label, Content)[0]);

    [Fact]
    public void LabelTravelsInClearRightAfterItsLength()
    {
        var envelope = Cipher.Seal(Label, Content);

        Assert.Equal(Label, Encoding.UTF8.GetString(envelope, 3, Label.Length));
        Assert.Equal(Label, EnvelopeCipher.LabelOf(envelope));
    }

    [Fact]
    public void ContentDoesNotTravelInClear()
    {
        var envelope = Cipher.Seal(Label, Content);

        Assert.Equal(-1, envelope.AsSpan().IndexOf(Content));
    }

    [Fact]
    public void EnvelopeHasTheSizeOfItsHeaderNonceContentAndTag() =>
        Assert.Equal(HeaderLength + 12 + Content.Length + 16, Cipher.Seal(Label, Content).Length);

    [Fact]
    public void SameContentSealedTwiceGivesTwoEnvelopes()
    {
        var first = Cipher.Seal(Label, Content);
        var second = Cipher.Seal(Label, Content);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void EnvelopeOfALabelIsRefusedUnderAnotherNamingOnlyTheOneAsked()
    {
        var envelope = Cipher.Seal(Label, Content);

        var refused = Assert.Throws<EnvelopeRefusedException>(() => Cipher.Open(Other, envelope));

        Assert.Contains(Other, refused.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Label, refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HeaderChangedToAnotherLabelIsRefusedEvenWhenOpenedUnderThatLabel()
    {
        var envelope = Cipher.Seal(Label, Content);
        Encoding.UTF8.GetBytes(Other).CopyTo(envelope, 3);

        _ = Assert.Throws<EnvelopeRefusedException>(() => Cipher.Open(Other, envelope));
    }

    [Fact]
    public void ContentSealedWithTheRightKeyButWithoutTheHeaderAsAssociatedDataIsRefused()
    {
        var forged = Cipher.Seal(Label, Content);
        using var key = new KeyDerivation(master, DerivationLabels.Of([Label])).Derive(Label);
        using var aes = new AesGcm(key.AsSpan(), 16);
        aes.Encrypt(forged.AsSpan(HeaderLength, 12), Content, forged.AsSpan(HeaderLength + 12, Content.Length), forged.AsSpan(^16..));

        _ = Assert.Throws<EnvelopeRefusedException>(() => Cipher.Open(Label, forged));
    }

    [Fact]
    public void ContentSealedWithTheRightKeyAndTheHeaderAsAssociatedDataIsOpened()
    {
        var forged = Cipher.Seal(Label, Content);
        using var key = new KeyDerivation(master, DerivationLabels.Of([Label])).Derive(Label);
        using var aes = new AesGcm(key.AsSpan(), 16);
        aes.Encrypt(forged.AsSpan(HeaderLength, 12), Content, forged.AsSpan(HeaderLength + 12, Content.Length), forged.AsSpan(^16..), forged.AsSpan(..HeaderLength));

        Assert.Equal(Content, Cipher.Open(Label, forged));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(HeaderLength - 1)]
    [InlineData(HeaderLength)]
    [InlineData(HeaderLength + 12)]
    public void EnvelopeWithAnyByteChangedIsRefused(int position)
    {
        var envelope = Cipher.Seal(Label, Content);
        envelope[position] ^= 0x01;

        _ = Assert.Throws<EnvelopeRefusedException>(() => Cipher.Open(Label, envelope));
    }

    [Fact]
    public void EnvelopeWithTheTagChangedIsRefused()
    {
        var envelope = Cipher.Seal(Label, Content);
        envelope[^1] ^= 0x01;

        _ = Assert.Throws<EnvelopeRefusedException>(() => Cipher.Open(Label, envelope));
    }

    [Fact]
    public void EnvelopeWithTheContentTruncatedIsRefused()
    {
        var envelope = Cipher.Seal(Label, Content);

        _ = Assert.Throws<EnvelopeRefusedException>(() => Cipher.Open(Label, envelope.AsSpan(..^1)));
    }

    [Fact]
    public void EnvelopeOfAnotherInstallationIsRefused()
    {
        using var otherMaster = new MasterKey([.. SyntheticKey.Valid().Select(value => (byte)(value ^ 0xFF))]);
        var theirs = new EnvelopeCipher(new KeyDerivation(otherMaster, DerivationLabels.Of([Label]))).Seal(Label, Content);

        _ = Assert.Throws<EnvelopeRefusedException>(() => Cipher.Open(Label, theirs));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(30)]
    public void BytesTooShortToBeAnEnvelopeAreRefused(int length) =>
        _ = Assert.Throws<EnvelopeRefusedException>(() => Cipher.Open(Label, new byte[length]));

    [Fact]
    public void VersionNotKnownIsRefusedSayingWhich()
    {
        var envelope = Cipher.Seal(Label, Content);
        envelope[0] = 2;

        var refused = Assert.Throws<EnvelopeRefusedException>(() => Cipher.Open(Label, envelope));

        Assert.Contains("version 2", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LabelLengthBeyondTheEnvelopeIsRefused()
    {
        var envelope = Cipher.Seal(Label, Content);
        envelope[1] = 0xFF;

        _ = Assert.Throws<EnvelopeRefusedException>(() => EnvelopeCipher.LabelOf(envelope));
    }

    [Fact]
    public void LabelNotLoadedIsNotSealed()
    {
        var narrow = new EnvelopeCipher(new KeyDerivation(master, DerivationLabels.None));

        _ = Assert.Throws<InvalidOperationException>(() => narrow.Seal(Label, Content));
    }

    [Fact]
    public void LabelNotLoadedIsNotOpened()
    {
        var envelope = Cipher.Seal(Label, Content);
        var narrow = new EnvelopeCipher(new KeyDerivation(master, DerivationLabels.None));

        _ = Assert.Throws<InvalidOperationException>(() => narrow.Open(Label, envelope));
    }

    [Fact]
    public void RefusalCarriesNeitherTheKeyNorTheContent()
    {
        var envelope = Cipher.Seal(Label, Content);
        envelope[^1] ^= 0x01;

        var refused = Assert.Throws<EnvelopeRefusedException>(() => Cipher.Open(Label, envelope));

        Assert.All(SyntheticKey.Spellings(SyntheticKey.Valid()), spelling => Assert.DoesNotContain(spelling, refused.ToString(), StringComparison.Ordinal));
        Assert.DoesNotContain("conteudo sintetico", refused.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void NoCipherWithoutDerivationIsADefect() =>
        _ = Assert.Throws<ArgumentNullException>(() => new EnvelopeCipher(null!));
}
