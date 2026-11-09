using System.Globalization;

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

    /// <summary>The event of the decision on a change of configuration, by a person other than the one who submitted; the item is the submission and the reference the sequence of its event.</summary>
    /// <param name="personId">The person who decided.</param>
    /// <param name="changeId">The submission decided.</param>
    /// <param name="submissionSequence">The sequence of the event of the submission.</param>
    /// <param name="approved">Whether the change was approved; refused otherwise.</param>
    public static CustodyEvent ConfigurationDecided(long personId, long changeId, long submissionSequence, bool approved)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(changeId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(submissionSequence);
        var step = approved ? CustodyStep.ConfigurationApproved : CustodyStep.ConfigurationRefused;
        return new CustodyEvent(CustodyAuthor.Person(personId), new CustodyItem(CustodyItemKind.ConfigurationChange, changeId), step, CustodyReason.AdministrativeAct)
        {
            Reference = submissionSequence.ToString(CultureInfo.InvariantCulture),
        };
    }

    /// <summary>The event of the core having started with the value an approved change asked for: by the system, on the submission, with the sequence of the approval.</summary>
    /// <param name="changeId">The submission in force.</param>
    /// <param name="approvalSequence">The sequence of the event that approved it.</param>
    public static CustodyEvent ConfigurationInForce(long changeId, long approvalSequence)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(changeId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(approvalSequence);
        var item = new CustodyItem(CustodyItemKind.ConfigurationChange, changeId);
        return new CustodyEvent(CustodyAuthor.System, item, CustodyStep.ConfigurationInForce, CustodyReason.AdministrativeAct)
        {
            Reference = approvalSequence.ToString(CultureInfo.InvariantCulture),
        };
    }

    /// <summary>The event of an approved change that will never be applied: by the system, on the submission, with the code of the reason and the sequence of the approval.</summary>
    /// <param name="changeId">The submission refused.</param>
    /// <param name="reasonCode">The code of the reason, in lower case and underscores.</param>
    /// <param name="approvalSequence">The sequence of the event that approved it.</param>
    public static CustodyEvent ConfigurationNotApplied(long changeId, string reasonCode, long approvalSequence)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(changeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(approvalSequence);
        var item = new CustodyItem(CustodyItemKind.ConfigurationChange, changeId);
        return new CustodyEvent(CustodyAuthor.System, item, CustodyStep.ConfigurationNotApplied, CustodyReason.AdministrativeAct)
        {
            Reference = reasonCode + "/" + approvalSequence.ToString(CultureInfo.InvariantCulture),
        };
    }
}
