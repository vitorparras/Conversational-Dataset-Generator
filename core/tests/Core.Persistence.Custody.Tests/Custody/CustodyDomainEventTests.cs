using Core.Persistence.Custody;

namespace Core.Persistence.Tests.Custody;

/// <summary>The event of a domain created (issue 54): the system's, about the number of the domain, for an administrative act, and in no other shape.</summary>
public sealed class CustodyDomainEventTests
{
    [Fact]
    public void CreationOfADomainIsTheSystemsAboutThatDomainForAnAdministrativeActWithNoReference()
    {
        var custodyEvent = CustodyEvent.DomainCreated(7);

        Assert.Equal(
            (CustodyAuthor.System, new CustodyItem(CustodyItemKind.Domain, 7), CustodyStep.DomainCreated, CustodyReason.AdministrativeAct, null, null),
            (custodyEvent.Author, custodyEvent.Item, custodyEvent.Step, custodyEvent.Reason, custodyEvent.Reference, custodyEvent.Corrects));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreationOfADomainWithoutAPositiveNumberIsRefused(int domainId) =>
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEvent.DomainCreated(domainId));

    [Fact]
    public void CreationOfADomainAsAPlainEventIsRefused() =>
        _ = Assert.Throws<ArgumentException>(
            () => CustodyEvent.Of(CustodyAuthor.Person(5), new CustodyItem(CustodyItemKind.Domain, 9), CustodyStep.DomainCreated, CustodyReason.AdministrativeAct));

    [Fact]
    public void TheKindAndTheStepAreWrittenUnderTheirNames() =>
        Assert.Equal(("domain", "domain_created"), (CustodyNames.Of(CustodyItemKind.Domain), CustodyNames.Of(CustodyStep.DomainCreated)));
}
