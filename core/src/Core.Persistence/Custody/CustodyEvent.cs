namespace Core.Persistence.Custody;

/// <summary>One event to add to the chain: who, about which item, which step and which reason, and the earlier event it corrects when it is a correction.</summary>
public sealed record CustodyEvent
{
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
        return step is CustodyStep.Correction or CustodyStep.ItemEmptied or CustodyStep.ItemEmptyingRefused || IsVerification(step)
            ? throw new ArgumentException(
                "A correction, a verification and an emptying have an event of their own; use Correction, Verification, GuaranteesVerified, Emptied or EmptyingRefused.", nameof(step))
            : new CustodyEvent(author, item, step, reason);
    }

    /// <summary>The event of an item emptied: a person did it, for one of the two reasons an emptying may be asked for, and the protocol that asked is the reference.</summary>
    /// <param name="author">The person who emptied; the system cannot.</param>
    /// <param name="itemId">The item of the content that was emptied.</param>
    /// <param name="reason">Either <see cref="CustodyReason.RemovalRequestedBySubject" /> or <see cref="CustodyReason.ContractRequirement" />.</param>
    /// <param name="reference">The number of the protocol outside the system; see <see cref="Reference" />.</param>
    public static CustodyEvent Emptied(CustodyAuthor author, long itemId, CustodyReason reason, string reference)
    {
        ArgumentNullException.ThrowIfNull(author);
        return reason is CustodyReason.RemovalRequestedBySubject or CustodyReason.ContractRequirement && author.PersonId is not null
            ? WithReference(new CustodyEvent(author, EmptyingItem(itemId), CustodyStep.ItemEmptied, reason), reference)
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
            ? WithReference(new CustodyEvent(author, EmptyingItem(itemId), CustodyStep.ItemEmptyingRefused, reason), reference)
            : throw new ArgumentException("An emptying is refused because its author does not hold the function or because the item was emptied already.", nameof(reason));
    }

    private static bool IsVerification(CustodyStep step) => step is CustodyStep.VerifyChain or CustodyStep.VerifyGuarantees or CustodyStep.OwnerWindowOpened or CustodyStep.OwnerWindowClosed or CustodyStep.Restored;

    internal static CustodyEvent OwnerWindowOpened(int to, string reference) =>
        new(CustodyAuthor.System, new CustodyItem(CustodyItemKind.CustodyChain, to), CustodyStep.OwnerWindowOpened, CustodyReason.AdministrativeAct) { Reference = reference };

    internal static CustodyEvent OwnerWindowClosed(long missing, string reference)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(missing);
        var reason = missing == 0 ? CustodyReason.VerificationPassed : CustodyReason.VerificationFailed;
        return new CustodyEvent(CustodyAuthor.System, new CustodyItem(CustodyItemKind.CustodyChain, missing), CustodyStep.OwnerWindowClosed, reason) { Reference = reference };
    }

    internal static CustodyEvent Restored(long rows, bool held, string reference)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rows);
        var reason = held ? CustodyReason.VerificationPassed : CustodyReason.VerificationFailed;
        return new CustodyEvent(CustodyAuthor.System, new CustodyItem(CustodyItemKind.CustodyChain, rows), CustodyStep.Restored, reason) { Reference = reference };
    }

    private static CustodyItem EmptyingItem(long itemId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(itemId);
        return new CustodyItem(CustodyItemKind.ItemText, itemId);
    }

    private static CustodyEvent WithReference(CustodyEvent custodyEvent, string reference) =>
        ProtocolReference.IsValid(reference)
            ? custodyEvent with { Reference = reference }
            : throw new ArgumentException("The reference is a protocol number of letters, digits and . _ / - only, up to 64 characters.", nameof(reference));

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

    /// <summary>The event that records the verdict of a conference of the guarantees of the server, by the system: the item is the chain and its id is the mask of the guarantees missing.</summary>
    /// <param name="missing">The mask of the guarantees missing, between 0 (every one held) and 63.</param>
    public static CustodyEvent GuaranteesVerified(long missing)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(missing);
        var reason = missing == 0 ? CustodyReason.VerificationPassed : CustodyReason.VerificationFailed;
        return new CustodyEvent(CustodyAuthor.System, new CustodyItem(CustodyItemKind.CustodyChain, missing), CustodyStep.VerifyGuarantees, reason);
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
