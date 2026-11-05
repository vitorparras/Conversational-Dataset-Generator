using Core.Persistence.Custody;

namespace Core.Persistence.Tests.Custody;

/// <summary>The event of a label revoked (issue 56): the system's, about the domain, for an administrative act, carrying the label as the reference and nothing else.</summary>
public sealed class CustodyDomainLabelRevokedEventTests
{
    [Theory]
    [InlineData(7, "texto-bruto/7/v1")]
    [InlineData(12, "mapa-reversao/12/v3")]
    public void TheRevocationIsTheSystemsAboutThatDomainForAnAdministrativeActWithTheLabelAsTheReference(int domainId, string label)
    {
        var revoked = CustodyEvent.DomainLabelRevoked(domainId, label);

        Assert.Equal(
            (CustodyAuthor.System, new CustodyItem(CustodyItemKind.Domain, domainId), CustodyStep.DomainLabelRevoked, CustodyReason.AdministrativeAct, label, null),
            (revoked.Author, revoked.Item, revoked.Step, revoked.Reason, revoked.Reference, revoked.Corrects));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ARevocationWithoutAPositiveDomainNumberIsRefusedByTheDomainParameter(int domainId)
    {
        var refused = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEvent.DomainLabelRevoked(domainId, "texto-bruto/1/v1"));

        Assert.Equal("domainId", refused.ParamName);
    }

    [Fact]
    public void ARevocationWithoutALabelIsRefusedByTheLabelParameter()
    {
        var refused = Assert.Throws<ArgumentException>(() => CustodyEvent.DomainLabelRevoked(1, string.Empty));

        Assert.Equal("label", refused.ParamName);
    }

    [Fact]
    public void ALabelThatIsNotAProtocolReferenceIsRefusedByTheReferenceParameter()
    {
        var refused = Assert.Throws<ArgumentException>(() => CustodyEvent.DomainLabelRevoked(1, "texto bruto/1/v1"));

        Assert.Equal("reference", refused.ParamName);
    }

    [Fact]
    public void TheRevocationAsAPlainEventIsRefused() =>
        _ = Assert.Throws<ArgumentException>(
            () => CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.Domain, 9), CustodyStep.DomainLabelRevoked, CustodyReason.AdministrativeAct));

    [Fact]
    public void TheStepIsWrittenUnderItsName() =>
        Assert.Equal("domain_label_revoked", CustodyNames.Of(CustodyStep.DomainLabelRevoked));
}
