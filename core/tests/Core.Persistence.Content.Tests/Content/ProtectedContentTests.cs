using System.Text;
using Core.Persistence.Content;
using Core.Platform.Keys;

namespace Core.Persistence.Tests.Content;

/// <summary>Protected content is sealed under the label of its purpose and domain, opened only for that purpose and domain, and never read from a value this path did not seal.</summary>
public sealed class ProtectedContentTests
{
    private const string Text = "texto sintetico do item";

    [Fact]
    public void SealedTextOpensAsTheSameText()
    {
        using var custody = SyntheticCustody.Holding("texto-bruto/7/v1");
        var sealedText = custody.Content.Seal(ContentColumns.RawText, 7, Text);

        var opened = custody.Content.Open(ContentColumns.RawText, 7, sealedText.Envelope.Span);

        Assert.Equal(Text, opened);
    }

    [Fact]
    public void EmptyTextOpensAsEmptyText()
    {
        using var custody = SyntheticCustody.Holding("texto-bruto/7/v1");
        var sealedText = custody.Content.Seal(ContentColumns.RawText, 7, string.Empty);

        Assert.Equal(string.Empty, custody.Content.Open(ContentColumns.RawText, 7, sealedText.Envelope.Span));
    }

    [Fact]
    public void SealedContentRemembersTheColumnAndTheDomainItWasSealedFor()
    {
        using var custody = SyntheticCustody.Holding("texto-bruto/7/v1");

        var sealedText = custody.Content.Seal(ContentColumns.SanitizedText, 7, Text);

        Assert.Equal((ContentColumns.SanitizedText, 7), (sealedText.Column, sealedText.DomainId));
    }

    [Fact]
    public void TextDoesNotTravelInClear()
    {
        using var custody = SyntheticCustody.Holding("texto-bruto/7/v1");

        var envelope = custody.Content.Seal(ContentColumns.RawText, 7, Text).Envelope.ToArray();

        Assert.DoesNotContain(Text, Encoding.Latin1.GetString(envelope), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("raw_text", "texto-bruto/7/v1")]
    [InlineData("sanitized_text", "texto-bruto/7/v1")]
    [InlineData("entries", "mapa-reversao/7/v1")]
    public void LabelOfTheEnvelopeIsThePurposeOfTheColumnAndTheDomain(string name, string label)
    {
        using var custody = SyntheticCustody.Holding("texto-bruto/7/v1", "mapa-reversao/7/v1");
        var column = ContentColumns.Protected.Single(candidate => candidate.Name == name);

        var envelope = custody.Content.Seal(column, 7, Text).Envelope.ToArray();

        Assert.Equal(label, EnvelopeCipher.LabelOf(envelope));
    }

    [Fact]
    public void SealingUsesTheNewestGenerationLoadedForTheDomain()
    {
        using var custody = SyntheticCustody.Holding("texto-bruto/7/v1", "texto-bruto/7/v10", "texto-bruto/7/v2", "texto-bruto/8/v11");

        var envelope = custody.Content.Seal(ContentColumns.RawText, 7, Text).Envelope.ToArray();

        Assert.Equal("texto-bruto/7/v10", EnvelopeCipher.LabelOf(envelope));
    }

    [Fact]
    public void ContentSealedBeforeARotationOpensUnderTheLabelItCarries()
    {
        byte[] old;
        using (var before = SyntheticCustody.Holding("texto-bruto/7/v1"))
        {
            old = before.Content.Seal(ContentColumns.RawText, 7, Text).Envelope.ToArray();
        }

        using var after = SyntheticCustody.Holding("texto-bruto/7/v1", "texto-bruto/7/v2");

        Assert.Equal(Text, after.Content.Open(ContentColumns.RawText, 7, old));
    }

    [Fact]
    public void SealingForADomainWithNoLabelOfThePurposeIsRefused()
    {
        using var custody = SyntheticCustody.Holding("texto-bruto/7/v1", "mapa-reversao/8/v1");

        _ = Assert.Throws<InvalidOperationException>(() => custody.Content.Seal(ContentColumns.RawText, 8, Text));
    }

    [Fact]
    public void ContentOfAnotherDomainIsRefused()
    {
        using var custody = SyntheticCustody.Holding("texto-bruto/7/v1", "texto-bruto/8/v1");
        var envelope = custody.Content.Seal(ContentColumns.RawText, 7, Text).Envelope.ToArray();

        _ = Assert.Throws<EnvelopeRefusedException>(() => custody.Content.Open(ContentColumns.RawText, 8, envelope));
    }

    [Fact]
    public void ContentOfAnotherPurposeIsRefused()
    {
        using var custody = SyntheticCustody.Holding("texto-bruto/7/v1", "mapa-reversao/7/v1");
        var envelope = custody.Content.Seal(ContentColumns.Entries, 7, Text).Envelope.ToArray();

        _ = Assert.Throws<EnvelopeRefusedException>(() => custody.Content.Open(ContentColumns.RawText, 7, envelope));
    }

    [Fact]
    public void TextStoredInClearInAProtectedColumnIsRefusedNotReadAsContent()
    {
        using var custody = SyntheticCustody.Holding("texto-bruto/7/v1");
        var clear = Encoding.UTF8.GetBytes(Text);

        _ = Assert.Throws<EnvelopeRefusedException>(() => custody.Content.Open(ContentColumns.RawText, 7, clear));
    }

    [Fact]
    public void EnvelopeWithAChangedByteIsRefused()
    {
        using var custody = SyntheticCustody.Holding("texto-bruto/7/v1");
        var envelope = custody.Content.Seal(ContentColumns.RawText, 7, Text).Envelope.ToArray();
        envelope[^1] ^= 0xFF;

        _ = Assert.Throws<EnvelopeRefusedException>(() => custody.Content.Open(ContentColumns.RawText, 7, envelope));
    }

    [Fact]
    public void LabelLoadedButWrittenOutsideTheFormatIsNeverUsedToSeal()
    {
        using var custody = SyntheticCustody.Holding("texto-bruto/07/v1");

        _ = Assert.Throws<InvalidOperationException>(() => custody.Content.Seal(ContentColumns.RawText, 7, Text));
    }

    [Fact]
    public void NullArgumentsAreRefusedByName()
    {
        using var custody = SyntheticCustody.Holding("texto-bruto/7/v1");
        var cipher = custody.Cipher;
        var labels = DerivationLabels.None;
        var content = custody.Content;

        Assert.Equal(
            ["cipher", "loaded", "column", "text", "column"],
            [
                Assert.Throws<ArgumentNullException>(() => new ProtectedContent(null!, labels)).ParamName,
                Assert.Throws<ArgumentNullException>(() => new ProtectedContent(cipher, null!)).ParamName,
                Assert.Throws<ArgumentNullException>(() => content.Seal(null!, 7, Text)).ParamName,
                Assert.Throws<ArgumentNullException>(() => content.Seal(ContentColumns.RawText, 7, null!)).ParamName,
                Assert.Throws<ArgumentNullException>(() => content.Open(null!, 7, [])).ParamName,
            ]);
    }

    [Fact]
    public void RefusalOfAnotherColumnNamesTheColumn()
    {
        using var custody = SyntheticCustody.Holding("texto-bruto/7/v1", "mapa-reversao/7/v1");
        var envelope = custody.Content.Seal(ContentColumns.Entries, 7, Text).Envelope.ToArray();

        var refused = Assert.Throws<EnvelopeRefusedException>(() => custody.Content.Open(ContentColumns.RawText, 7, envelope));

        Assert.Equal("The envelope was not sealed for the purpose and the domain of item_text.raw_text.", refused.Message);
    }

    [Fact]
    public void SealingWithNoLabelSaysThePurposeAndTheDomain()
    {
        using var custody = SyntheticCustody.Holding("texto-bruto/7/v1");

        var refused = Assert.Throws<InvalidOperationException>(() => custody.Content.Seal(ContentColumns.RawText, 8, Text));

        Assert.Equal("No derivation label is loaded for the purpose RawText and the domain 8.", refused.Message);
    }

    [Fact]
    public void EnvelopeWhoseLabelIsNotALabelIsRefusedAsSuch()
    {
        using var custody = SyntheticCustody.Holding("not-a-label");
        var envelope = custody.Cipher.Seal("not-a-label", "x"u8);

        var refused = Assert.Throws<EnvelopeRefusedException>(() => custody.Content.Open(ContentColumns.RawText, 7, envelope));

        Assert.Equal("The label in the header is not a derivation label.", refused.Message);
    }

    [Fact]
    public void EnvelopeOfAGenerationNotLoadedIsRefusedNotFailedAsADefect()
    {
        byte[] future;
        using (var rotated = SyntheticCustody.Holding("texto-bruto/7/v1", "texto-bruto/7/v2"))
        {
            future = rotated.Content.Seal(ContentColumns.RawText, 7, Text).Envelope.ToArray();
        }

        using var custody = SyntheticCustody.Holding("texto-bruto/7/v1");
        var refused = Assert.Throws<EnvelopeRefusedException>(() => custody.Content.Open(ContentColumns.RawText, 7, future));

        Assert.Equal("The label 'texto-bruto/7/v2' of the envelope is not among the loaded ones.", refused.Message);
    }
}
