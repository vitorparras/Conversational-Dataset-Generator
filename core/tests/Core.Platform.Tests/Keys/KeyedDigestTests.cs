using Core.Platform.Keys;

namespace Core.Platform.Tests.Keys;

/// <summary>
/// The keyed digest of a text (issue 82): always the same for the same key, purpose and text; another for another key, purpose or text; never the text; and never under a purpose that is a label.
/// </summary>
public sealed class KeyedDigestTests : IDisposable
{
    private readonly MasterKey master = new(SyntheticKey.Valid());

    private KeyDerivation Derivation => new(master, DerivationLabels.None);

    public void Dispose() => master.Dispose();

    [Fact]
    public void TheSameKeyPurposeAndTextGiveTheSameDigest() =>
        Assert.Equal(Derivation.Digest("replicacao-texto/7", "ZQX-conteudo"), Derivation.Digest("replicacao-texto/7", "ZQX-conteudo"));

    [Fact]
    public void AnotherPurposeOrAnotherTextGivesAnotherDigest()
    {
        var digest = Derivation.Digest("replicacao-texto/7", "ZQX-conteudo");

        Assert.NotEqual(digest, Derivation.Digest("replicacao-texto/8", "ZQX-conteudo"));
        Assert.NotEqual(digest, Derivation.Digest("replicacao-remetente/7", "ZQX-conteudo"));
        Assert.NotEqual(digest, Derivation.Digest("replicacao-texto/7", "ZQX-conteudo "));
    }

    [Fact]
    public void AnotherMasterKeyGivesAnotherDigest()
    {
        var other = Enumerable.Repeat((byte)7, MasterKey.Length).ToArray();
        using var reading = MasterKeyReading.FromBytes(other, DerivationLabels.None);

        Assert.NotEqual(Derivation.Digest("replicacao-texto/7", "ZQX-conteudo"), new KeyDerivation(reading.Key!, DerivationLabels.None).Digest("replicacao-texto/7", "ZQX-conteudo"));
    }

    [Fact]
    public void TheDigestIs64LowercaseHexadecimalCharactersAndShowsNothingOfTheText()
    {
        var digest = Derivation.Digest("replicacao-texto/7", "ZQX-conteudo");

        Assert.Matches("^[0-9a-f]{64}$", digest);
        Assert.DoesNotContain("zqx", digest, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("texto-bruto/7/v1")]
    [InlineData("mapa-reversao/1/v2")]
    public void APurposeThatIsTheTextOfALabelIsRefusedSoThatNoDigestSharesAKeyWithAContentKey(string label) =>
        _ = Assert.Throws<ArgumentException>(() => Derivation.Digest(label, "ZQX-conteudo"));

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void ABlankPurposeIsRefused(string purpose) =>
        _ = Assert.ThrowsAny<ArgumentException>(() => Derivation.Digest(purpose, "ZQX-conteudo"));
}
