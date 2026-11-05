namespace Core.Persistence.Custody;

public sealed partial record CustodyEvent
{
    /// <summary>The event of a batch of keys of a domain wrapped again, by the system; the item is the domain, and the event has no reference.</summary>
    public static CustodyEvent DomainRewrapped(int domainId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(domainId);
        return new CustodyEvent(CustodyAuthor.System, new CustodyItem(CustodyItemKind.Domain, domainId), CustodyStep.DomainRewrapped, CustodyReason.AdministrativeAct);
    }
}
