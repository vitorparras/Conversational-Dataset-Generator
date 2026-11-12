using System.Collections.Frozen;

namespace Core.Persistence.Custody;

/// <summary>The steps that have a factory of their own in <see cref="CustodyEvent" /> and cannot be recorded as a plain event, and the message that says so.</summary>
internal static class EventsOfTheirOwn
{
    internal const string Message =
        "A correction, a verification, an emptying, a rotation, the creation of a domain, a seal, an entry, the loss of a key, a rehearsal of the recovery, "
        + "a conference of the guarantees, a window of the owner, a restore, a rehearsal of it, a revocation of a label, a rewrap of a domain, "
        + "the opening, the sample, the review and the closing of a batch "
        + "and the submission of a change of configuration, the decision on it and its being in force and its never being applied have an event of their own; "
        + "use Correction, Verification, Emptied, EmptyingRefused, EpochRotated, DomainCreated, DomainLabelRotated, DomainLabelRevoked, DomainRewrapped, SealRecorded, SignInAccepted, "
        + "SignInRefused, KeyLossDeclared, RecoveryRehearsed, GuaranteesVerified, OwnerWindowOpened, OwnerWindowClosed, Restored, RestoreRehearsed, BatchOpened, BatchAccepted, "
        + "BatchRejected, BatchSampled, BatchReviewed, ConfigurationSubmitted, ConfigurationDecided, ConfigurationInForce or ConfigurationNotApplied.";

    private static readonly FrozenSet<CustodyStep> Steps = new HashSet<CustodyStep>
    {
        CustodyStep.Correction, CustodyStep.VerifyChain, CustodyStep.ItemEmptied, CustodyStep.ItemEmptyingRefused, CustodyStep.EpochRotated, CustodyStep.DomainCreated,
        CustodyStep.SealRecorded, CustodyStep.DomainLabelRotated, CustodyStep.SignInAccepted, CustodyStep.SignInRefused, CustodyStep.KeyLossDeclared, CustodyStep.RecoveryTested,
        CustodyStep.VerifyGuarantees, CustodyStep.OwnerWindowOpened, CustodyStep.OwnerWindowClosed, CustodyStep.Restored, CustodyStep.RestoreRehearsed,
        CustodyStep.DomainLabelRevoked, CustodyStep.DomainRewrapped, CustodyStep.BatchOpened, CustodyStep.BatchClosed, CustodyStep.PasswordChanged,
        CustodyStep.ConfigurationSubmitted, CustodyStep.ConfigurationApproved, CustodyStep.ConfigurationRefused, CustodyStep.ConfigurationInForce, CustodyStep.ConfigurationNotApplied,
        CustodyStep.BatchSampled, CustodyStep.BatchReviewed,
    }.ToFrozenSet();

    /// <summary>Whether the step has a factory of its own.</summary>
    /// <param name="step">The step asked for as a plain event.</param>
    internal static bool Has(CustodyStep step) => Steps.Contains(step);
}
