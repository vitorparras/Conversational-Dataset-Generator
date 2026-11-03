using System.Collections.Frozen;

namespace Core.Persistence.Custody;

/// <summary>One event to add to the chain: who, about which item, which step and which reason, and the earlier event it corrects when it is a correction.</summary>
public sealed record CustodyEvent
{
    private static readonly FrozenSet<CustodyStep> OfTheirOwn = Enumerable.Repeat(CustodyStep.Correction, 1)
        .Append(CustodyStep.VerifyChain).Append(CustodyStep.ItemEmptied).Append(CustodyStep.ItemEmptyingRefused).Append(CustodyStep.EpochRotated).Append(CustodyStep.DomainCreated)
        .Append(CustodyStep.SealRecorded).ToFrozenSet();

    private CustodyEvent(CustodyAuthor author, CustodyItem item, CustodyStep step, CustodyReason reason)
    {
        Author = author;
        Item = item;
        Step = step;
        Reason = reason;
    }

    /// <summary>Who did it.</summary>
    public CustodyAuthor Author { get; }

    /// <summary>What it was about.</summary>
    public CustodyItem Item { get; }

    /// <summary>The step recorded.</summary>
    public CustodyStep Step { get; }

    /// <summary>Why.</summary>
    public CustodyReason Reason { get; }

    /// <summary>The sequence of the event this one corrects; null when it corrects none.</summary>
    public long? Corrects { get; private init; }

    /// <summary>The protocol outside the system that asked for what the event records; only an emptying, or the refusal of one, carries it.</summary>
    public string? Reference { get; private init; }

    /// <summary>An event that records a step; the correction of an event has its own constructor.</summary>
    /// <param name="author">Who did it.</param>
    /// <param name="item">What it was about.</param>
    /// <param name="step">Any step but <see cref="CustodyStep.Correction" /> and <see cref="CustodyStep.VerifyChain" />.</param>
    /// <param name="reason">Why.</param>
    public static CustodyEvent Of(CustodyAuthor author, CustodyItem item, CustodyStep step, CustodyReason reason)
    {
        ArgumentNullException.ThrowIfNull(author);
        ArgumentNullException.ThrowIfNull(item);
        return HasEventOfItsOwn(step)
            ? throw new ArgumentException(
                "A correction, a verification, an emptying, a rotation, the creation of a domain and a seal have an event of their own; "
                + "use Correction, Verification, Emptied, EmptyingRefused, EpochRotated, DomainCreated or SealRecorded.", nameof(step))
            : new CustodyEvent(author, item, step, reason);
    }

    private static bool HasEventOfItsOwn(CustodyStep step) => OfTheirOwn.Contains(step);

    /// <summary>The event of an item emptied: a person did it, for one of the two reasons an emptying may be asked for, and the protocol that asked is the reference.</summary>
    /// <param name="author">The person who emptied; the system cannot.</param>
    /// <param name="itemId">The item of the content that was emptied.</param>
    /// <param name="reason">Either <see cref="CustodyReason.RemovalRequestedBySubject" /> or <see cref="CustodyReason.ContractRequirement" />.</param>
    /// <param name="reference">The number of the protocol outside the system; see <see cref="Reference" />.</param>
    public static CustodyEvent Emptied(CustodyAuthor author, long itemId, CustodyReason reason, string reference)
    {
        ArgumentNullException.ThrowIfNull(author);
        return reason is CustodyReason.RemovalRequestedBySubject or CustodyReason.ContractRequirement && author.PersonId is not null
            ? OfEmptying(author, itemId, CustodyStep.ItemEmptied, reason, reference)
            : throw new ArgumentException("An emptying is done by a person, for the request of the subject or for a requirement of the contract.", nameof(reason));
    }

    /// <summary>The event of an emptying that was refused, about the same item, with the reason it was refused for.</summary>
    /// <param name="author">Who tried to empty.</param>
    /// <param name="itemId">The item of the content that stays as it was.</param>
    /// <param name="reason">Either <see cref="CustodyReason.AuthorWithoutFunction" /> or <see cref="CustodyReason.ItemAlreadyEmptied" />.</param>
    /// <param name="reference">The number of the protocol outside the system that asked.</param>
    public static CustodyEvent EmptyingRefused(CustodyAuthor author, long itemId, CustodyReason reason, string reference)
    {
        ArgumentNullException.ThrowIfNull(author);
        return reason is CustodyReason.AuthorWithoutFunction or CustodyReason.ItemAlreadyEmptied
            ? OfEmptying(author, itemId, CustodyStep.ItemEmptyingRefused, reason, reference)
            : throw new ArgumentException("An emptying is refused because its author does not hold the function or because the item was emptied already.", nameof(reason));
    }

    private static CustodyEvent OfEmptying(CustodyAuthor author, long itemId, CustodyStep step, CustodyReason reason, string reference)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(itemId);
        return ProtocolReference.IsValid(reference)
            ? new CustodyEvent(author, new CustodyItem(CustodyItemKind.ItemText, itemId), step, reason) { Reference = reference }
            : throw new ArgumentException("The reference is a protocol number of letters, digits and . _ / - only, up to 64 characters.", nameof(reference));
    }

    /// <summary>
    /// The event that records the verdict of a verification of the chain, written by the system: the item is the chain, its id is the number of events the verification read,
    /// and, when the verification failed, the event it points at is the first inconsistent one.
    /// </summary>
    /// <param name="eventsRead">How many events the verification read.</param>
    /// <param name="firstInconsistent">The sequence of the first inconsistent event; null when the chain held.</param>
    public static CustodyEvent Verification(long eventsRead, long? firstInconsistent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(eventsRead);
        if (firstInconsistent is { } sequence)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        }

        var reason = firstInconsistent is null ? CustodyReason.VerificationPassed : CustodyReason.VerificationFailed;
        return new CustodyEvent(CustodyAuthor.System, new CustodyItem(CustodyItemKind.CustodyChain, eventsRead), CustodyStep.VerifyChain, reason) { Corrects = firstInconsistent };
    }

    /// <summary>The event of a rotation of the epoch, written by the system: the keys of the content were wrapped again under the epoch given.</summary>
    /// <param name="epoch">The number of the epoch the keys are wrapped under now.</param>
    public static CustodyEvent EpochRotated(int epoch)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(epoch);
        return new CustodyEvent(CustodyAuthor.System, new CustodyItem(CustodyItemKind.ContentEpoch, epoch), CustodyStep.EpochRotated, CustodyReason.AdministrativeAct);
    }

    /// <summary>The event of a domain created, written by the system: the item is the domain, by its number, and the name of the domain is not in the event.</summary>
    /// <param name="domainId">The identifier the domain took in the registry.</param>
    public static CustodyEvent DomainCreated(int domainId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(domainId);
        return new CustodyEvent(CustodyAuthor.System, new CustodyItem(CustodyItemKind.Domain, domainId), CustodyStep.DomainCreated, CustodyReason.AdministrativeAct);
    }

    /// <summary>The event of a seal recorded: the item is the chain, its id is the sequence of the event sealed, and the digest of that event is read from the chain and is not in this one.</summary>
    /// <param name="author">The system, or the person who confirmed the record.</param>
    /// <param name="sealedSequence">The sequence of the last event the seal covers, which is also the number of events it counts; 0 for a chain with none.</param>
    public static CustodyEvent SealRecorded(CustodyAuthor author, long sealedSequence)
    {
        ArgumentNullException.ThrowIfNull(author);
        ArgumentOutOfRangeException.ThrowIfNegative(sealedSequence);
        return new CustodyEvent(author, new CustodyItem(CustodyItemKind.CustodyChain, sealedSequence), CustodyStep.SealRecorded, CustodyReason.AdministrativeAct);
    }

    /// <summary>A new event that corrects an earlier one, which stays in the sequence as it was.</summary>
    /// <param name="author">Who corrects.</param>
    /// <param name="item">What the corrected event was about.</param>
    /// <param name="corrected">The sequence of the event corrected.</param>
    public static CustodyEvent Correction(CustodyAuthor author, CustodyItem item, long corrected)
    {
        ArgumentNullException.ThrowIfNull(author);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(corrected);
        return new CustodyEvent(author, item, CustodyStep.Correction, CustodyReason.CorrectionOfRecord) { Corrects = corrected };
    }
}
