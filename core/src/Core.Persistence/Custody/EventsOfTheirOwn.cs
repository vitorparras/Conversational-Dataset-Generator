using System.Collections.Frozen;

namespace Core.Persistence.Custody;

/// <summary>The steps that have a factory of their own in <see cref="CustodyEvent" /> and cannot be recorded as a plain event, and the message that says so.</summary>
internal static class EventsOfTheirOwn
{
    internal const string Message =
        "A correction, a verification, an emptying, a rotation, the creation of a domain, a seal, an entry, the loss of a key, a rehearsal of the recovery, "
        + "a conference of the guarantees, a window of the owner, a restore, a rehearsal of it, a revocation of a label and a rewrap of a domain have an event of their own; "
        + "use Correction, Verification, Emptied, EmptyingRefused, EpochRotated, DomainCreated, DomainLabelRotated, DomainLabelRevoked, DomainRewrapped, SealRecorded, SignInAccepted, "
        + "SignInRefused, KeyLossDeclared, RecoveryRehearsed, GuaranteesVerified, OwnerWindowOpened, OwnerWindowClosed, Restored or RestoreRehearsed.";

    private static readonly FrozenSet<CustodyStep> Steps = Enumerable.Repeat(CustodyStep.Correction, 1)
        .Append(CustodyStep.VerifyChain).Append(CustodyStep.ItemEmptied).Append(CustodyStep.ItemEmptyingRefused).Append(CustodyStep.EpochRotated).Append(CustodyStep.DomainCreated)
        .Append(CustodyStep.SealRecorded).Append(CustodyStep.DomainLabelRotated)
        .Append(CustodyStep.SignInAccepted).Append(CustodyStep.SignInRefused).Append(CustodyStep.KeyLossDeclared).Append(CustodyStep.RecoveryTested)
        .Append(CustodyStep.VerifyGuarantees).Append(CustodyStep.OwnerWindowOpened).Append(CustodyStep.OwnerWindowClosed)
        .Append(CustodyStep.Restored).Append(CustodyStep.RestoreRehearsed).Append(CustodyStep.DomainLabelRevoked).Append(CustodyStep.DomainRewrapped).Append(CustodyStep.PasswordChanged).ToFrozenSet();

    /// <summary>Whether the step has a factory of its own.</summary>
    /// <param name="step">The step asked for as a plain event.</param>
    internal static bool Has(CustodyStep step) => Steps.Contains(step);
}
