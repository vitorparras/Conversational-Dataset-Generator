using Core.Persistence.Custody;

namespace Core.Persistence.Tests.Custody;

/// <summary>The event of a batch of keys wrapped again (issue 56): the system's, about the domain, for an administrative act, with no reference.</summary>
public sealed class CustodyDomainRewrappedEventTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    public void TheRewrapIsTheSystemsAboutThatDomainForAnAdministrativeActWithNoReference(int domainId)
    {
        var rewrapped = CustodyEvent.DomainRewrapped(domainId);

        Assert.Equal(
            (CustodyAuthor.System, new CustodyItem(CustodyItemKind.Domain, domainId), CustodyStep.DomainRewrapped, CustodyReason.AdministrativeAct, null, null),
            (rewrapped.Author, rewrapped.Item, rewrapped.Step, rewrapped.Reason, rewrapped.Reference, rewrapped.Corrects));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ARewrapWithoutAPositiveDomainNumberIsRefusedByTheDomainParameter(int domainId)
    {
        var refused = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEvent.DomainRewrapped(domainId));

        Assert.Equal("domainId", refused.ParamName);
    }

    [Fact]
    public void TheRewrapAsAPlainEventIsRefused() =>
        _ = Assert.Throws<ArgumentException>(
            () => CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.Domain, 9), CustodyStep.DomainRewrapped, CustodyReason.AdministrativeAct));

    [Fact]
    public void TheStepIsWrittenUnderItsName() =>
        Assert.Equal("domain_rewrapped", CustodyNames.Of(CustodyStep.DomainRewrapped));
}
