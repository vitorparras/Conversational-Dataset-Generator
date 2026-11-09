namespace Core.Persistence.Custody;

public sealed partial record CustodyEvent
{
    /// <summary>The event of a change of configuration submitted by a person; the item is the submission, and neither the key nor the value travels in the event.</summary>
    /// <param name="personId">The person who submitted the change.</param>
    /// <param name="changeId">The identifier the submission took.</param>
    public static CustodyEvent ConfigurationSubmitted(long personId, long changeId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(changeId);
        return new CustodyEvent(CustodyAuthor.Person(personId), new CustodyItem(CustodyItemKind.ConfigurationChange, changeId), CustodyStep.ConfigurationSubmitted, CustodyReason.AdministrativeAct);
    }
}
