using Core.Platform.Keys;

namespace Core.Platform.Tests.Keys;

/// <summary>The label is written as purpose, whole domain id and version, and read back only from exactly that form.</summary>
public sealed class DerivationLabelTests
{
    [Theory]
    [InlineData(LabelPurpose.RawText, 7, 1, "texto-bruto/7/v1")]
    [InlineData(LabelPurpose.ReversalMap, 12, 3, "mapa-reversao/12/v3")]
    [InlineData(LabelPurpose.RawText, int.MaxValue, int.MaxValue, "texto-bruto/2147483647/v2147483647")]
    public void LabelIsWrittenAsPurposeDomainAndVersion(LabelPurpose purpose, int domainId, int version, string text) =>
        Assert.Equal(text, new DerivationLabel(purpose, domainId, version).Text);

    [Theory]
    [InlineData("texto-bruto/7/v1", LabelPurpose.RawText, 7, 1)]
    [InlineData("mapa-reversao/12/v3", LabelPurpose.ReversalMap, 12, 3)]
    [InlineData("texto-bruto/2147483647/v2147483647", LabelPurpose.RawText, int.MaxValue, int.MaxValue)]
    public void TextOfALabelIsReadBack(string text, LabelPurpose purpose, int domainId, int version)
    {
        var read = DerivationLabel.TryParse(text, out var label);

        Assert.True(read);
        Assert.Equal(new DerivationLabel(purpose, domainId, version), label);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("texto-bruto")]
    [InlineData("texto-bruto/7")]
    [InlineData("texto-bruto/7/v1/x")]
    [InlineData("texto-sanitizado/7/v1")]
    [InlineData("Texto-Bruto/7/v1")]
    [InlineData("texto-bruto/0/v1")]
    [InlineData("texto-bruto/-7/v1")]
    [InlineData("texto-bruto/+7/v1")]
    [InlineData("texto-bruto/07/v1")]
    [InlineData("texto-bruto/7.0/v1")]
    [InlineData("texto-bruto/ 7/v1")]
    [InlineData("texto-bruto/٧/v1")]
    [InlineData("texto-bruto/2147483648/v1")]
    [InlineData("texto-bruto//v1")]
    [InlineData("texto-bruto/7/1")]
    [InlineData("texto-bruto/7/V1")]
    [InlineData("texto-bruto/7/v0")]
    [InlineData("texto-bruto/7/v01")]
    [InlineData("texto-bruto/7/v-1")]
    [InlineData("texto-bruto/7/v")]
    [InlineData("texto-bruto/7/v1 ")]
    [InlineData(" texto-bruto/7/v1")]
    public void TextThatIsNotExactlyALabelIsNotRead(string? text)
    {
        var read = DerivationLabel.TryParse(text, out var label);

        Assert.False(read);
        Assert.Null(label);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void DomainIdBelowOneIsADefect(int domainId) =>
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new DerivationLabel(LabelPurpose.RawText, domainId, 1));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void VersionBelowOneIsADefect(int version) =>
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new DerivationLabel(LabelPurpose.RawText, 1, version));

    [Fact]
    public void PurposeOutsideTheClosedSetIsADefect() =>
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new DerivationLabel((LabelPurpose)2, 1, 1));

    [Fact]
    public void ThePurposesAreExactlyTheTwoOfTheDecision() =>
        Assert.Equal([LabelPurpose.RawText, LabelPurpose.ReversalMap], Enum.GetValues<LabelPurpose>());

    [Fact]
    public void LabelsOfTheSameThreePartsAreEqual() =>
        Assert.Equal(new DerivationLabel(LabelPurpose.RawText, 7, 1), new DerivationLabel(LabelPurpose.RawText, 7, 1));

    [Fact]
    public void LabelOfAnotherDomainIsAnotherLabel() =>
        Assert.NotEqual(new DerivationLabel(LabelPurpose.RawText, 7, 1), new DerivationLabel(LabelPurpose.RawText, 8, 1));
}
