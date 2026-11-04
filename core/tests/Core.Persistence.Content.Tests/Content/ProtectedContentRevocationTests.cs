using Core.Persistence.Content;
using Core.Persistence.Content.Sealing;
using Core.Platform.Keys;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// A label revoked (issue 56, CA-4): the content written afterwards is sealed under the newest generation registered and not revoked, never under the revoked one, and what was sealed under
/// a revoked label opens all the same. A domain whose every generation of a purpose is revoked has nothing to seal under, and says so.
/// </summary>
public sealed class ProtectedContentRevocationTests
{
    private const string Text = "texto sintetico do item";

    [Fact]
    public void ANewRowIsNotSealedUnderARevokedLabelAndTakesTheNewestOneNotRevoked()
    {
        using var custody = SyntheticCustody.Holding("texto-bruto/7/v1", "texto-bruto/7/v2", "texto-bruto/7/v3");
        var content = ContentRevoking(custody, "texto-bruto/7/v3");

        Assert.Equal("texto-bruto/7/v2", LabelOfNewRow(content, 7));
    }

    [Fact]
    public void ContentSealedBeforeTheRevocationOpensUnderTheRevokedLabelItCarries()
    {
        using var custody = SyntheticCustody.Holding("texto-bruto/7/v1", "texto-bruto/7/v2");
        var old = custody.Seal(ContentColumns.RawText, 7, Text);
        var content = ContentRevoking(custody, "texto-bruto/7/v1");

        var opened = content.Open(ContentColumns.RawText, 7, old.Envelope.Span, old.Key);

        Assert.Equal((Text, "texto-bruto/7/v2"), (opened, LabelOfNewRow(content, 7)));
    }

    [Fact]
    public void ARevokedLabelOfOneDomainDoesNotChangeTheLabelOfAnother()
    {
        using var custody = SyntheticCustody.Holding("texto-bruto/7/v1", "texto-bruto/7/v2", "texto-bruto/8/v1", "texto-bruto/8/v2");
        var content = ContentRevoking(custody, "texto-bruto/7/v2");

        Assert.Equal(("texto-bruto/7/v1", "texto-bruto/8/v2"), (LabelOfNewRow(content, 7), LabelOfNewRow(content, 8)));
    }

    [Fact]
    public void ARevokedLabelOfOnePurposeDoesNotChangeTheOtherPurposeOfTheSameDomain()
    {
        using var custody = SyntheticCustody.Holding("texto-bruto/7/v1", "texto-bruto/7/v2", "mapa-reversao/7/v1", "mapa-reversao/7/v2");
        var content = ContentRevoking(custody, "texto-bruto/7/v2");

        using var sealing = content.Begin(ContentColumns.MapKey, 7);
        var map = sealing.Seal(ContentColumns.Entries, Text);

        Assert.Equal(("texto-bruto/7/v1", "mapa-reversao/7/v2"), (LabelOfNewRow(content, 7), EnvelopeCipher.LabelOf(map.Envelope.ToArray())));
    }

    [Fact]
    public void WhenEveryGenerationOfThePurposeIsRevokedThereIsNothingToSealUnder()
    {
        using var custody = SyntheticCustody.Holding("texto-bruto/7/v1", "texto-bruto/7/v2");
        var content = ContentRevoking(custody, "texto-bruto/7/v1", "texto-bruto/7/v2");

        _ = Assert.Throws<InvalidOperationException>(() => content.Begin(ContentColumns.ItemKey, 7));
    }

    [Fact]
    public void WithNothingRevokedTheNewestGenerationIsUsedAsBefore()
    {
        using var custody = SyntheticCustody.Holding("texto-bruto/7/v1", "texto-bruto/7/v2");
        var content = ContentRevoking(custody);

        Assert.Equal("texto-bruto/7/v2", LabelOfNewRow(content, 7));
    }

    [Fact]
    public void ThePathNeedsTheSetOfTheLabelsRevoked()
    {
        using var custody = SyntheticCustody.Holding("texto-bruto/7/v1");

        var refused = Assert.Throws<ArgumentNullException>(() => new ProtectedContent(custody.Labels, custody.Keys, new HashSet<string>(), null!));

        Assert.Equal("revoked", refused.ParamName);
    }

    private static ProtectedContent ContentRevoking(SyntheticCustody custody, params string[] revoked) =>
        new(custody.Labels, custody.Keys, new HashSet<string>(custody.Labels.All, StringComparer.Ordinal), new HashSet<string>(revoked, StringComparer.Ordinal));

    private static string LabelOfNewRow(ProtectedContent content, int domainId)
    {
        using var sealing = content.Begin(ContentColumns.ItemKey, domainId);
        return EnvelopeCipher.LabelOf(sealing.Seal(ContentColumns.RawText, Text).Envelope.ToArray());
    }
}
