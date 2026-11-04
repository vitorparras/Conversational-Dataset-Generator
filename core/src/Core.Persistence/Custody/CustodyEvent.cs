using System.Collections.Frozen;

namespace Core.Persistence.Custody;

/// <summary>One event to add to the chain: who, about which item, which step and which reason, and the earlier event it corrects when it is a correction.</summary>
public sealed record CustodyEvent
{
    private const int FirstRotation = 2;

    private static readonly FrozenSet<CustodyStep> OfTheirOwn = Enumerable.Repeat(CustodyStep.Correction, 1)
        .Append(CustodyStep.VerifyChain).Append(CustodyStep.ItemEmptied).Append(CustodyStep.ItemEmptyingRefused).Append(CustodyStep.EpochRotated).Append(CustodyStep.DomainCreated)
        .Append(CustodyStep.SealRecorded).Append(CustodyStep.DomainLabelRotated)
        .Append(CustodyStep.SignInAccepted).Append(CustodyStep.SignInRefused).Append(CustodyStep.KeyLossDeclared).Append(CustodyStep.RecoveryTested)
        .Append(CustodyStep.VerifyGuarantees).ToFrozenSet();

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
                "A correction, a verification, an emptying, a rotation, the creation of a domain, a seal and an entry have an event of their own; "
                + "use Correction, Verification, GuaranteesVerified, Emptied, EmptyingRefused, EpochRotated, DomainCreated, SealRecorded, DomainLabelRotated, SignInAccepted or SignInRefused.",
                nameof(step))
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

    /// <summary>The event of a domain that passed to write under the next generation of its labels, by the system; the reference is the generation, as <c>v2</c>.</summary>
    public static CustodyEvent DomainLabelRotated(int domainId, int version)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(domainId);
        ArgumentOutOfRangeException.ThrowIfLessThan(version, FirstRotation);
        return new CustodyEvent(CustodyAuthor.System, new CustodyItem(CustodyItemKind.Domain, domainId), CustodyStep.DomainLabelRotated, CustodyReason.AdministrativeAct) { Reference = $"v{version}" };
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

    /// <summary>An attempt to sign in that was accepted: the person is the author and the item is the attempt.</summary>
    public static CustodyEvent SignInAccepted(long personId, long attemptId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attemptId);
        return new CustodyEvent(CustodyAuthor.Person(personId), new CustodyItem(CustodyItemKind.SignInAttempt, attemptId), CustodyStep.SignInAccepted, CustodyReason.CredentialsAccepted);
    }

    /// <summary>An attempt to sign in that was refused: the author is the person only for a wrong password (<paramref name="personId"/>), and the system for any other reason.</summary>
    public static CustodyEvent SignInRefused(long? personId, CustodyReason reason, long attemptId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attemptId);
        return RefusalAuthor(personId, reason) is { } author
            ? new CustodyEvent(author, new CustodyItem(CustodyItemKind.SignInAttempt, attemptId), CustodyStep.SignInRefused, reason)
            : throw new ArgumentException("An entry is refused for a wrong password, of a person, or for a name nobody goes by, of nobody.", nameof(reason));
    }

    private static CustodyAuthor? RefusalAuthor(long? personId, CustodyReason reason)
    {
        if (reason == CustodyReason.PasswordWrong && personId is { } person)
        {
            return CustodyAuthor.Person(person);
        }

        return reason == CustodyReason.AccountNotRecognized && personId is null ? CustodyAuthor.System : null;
    }

    /// <summary>
    /// The event of the loss of a key declared by whoever operates, written by the system: the item is the master key, its id is what was tried (the sum of <see cref="KeyLossAttempts" />),
    /// and the reference is the scope. There is no way back: what was sealed under the scope stays unreadable.
    /// </summary>
    /// <param name="scope">The whole master key, or one label.</param>
    /// <param name="attempts">What was tried to recover it; at least one.</param>
    public static CustodyEvent KeyLossDeclared(KeyLossScope scope, KeyLossAttempts attempts)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero((int)attempts);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((int)attempts, (int)KeyLossAttempts.All);
        return WithReference(
            new CustodyEvent(CustodyAuthor.System, new CustodyItem(CustodyItemKind.MasterKey, (long)attempts), CustodyStep.KeyLossDeclared, CustodyReason.AdministrativeAct),
            scope.Text);
    }

    /// <summary>The event of a recovery rehearsed and checked, written by the system: the item is the master key and its id is the sequence of the top of the copy the rehearsal opened.</summary>
    /// <param name="topSequence">The sequence of the last event of the copy, which the chain of the production holds too.</param>
    public static CustodyEvent RecoveryRehearsed(long topSequence)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(topSequence);
        return new CustodyEvent(CustodyAuthor.System, new CustodyItem(CustodyItemKind.MasterKey, topSequence), CustodyStep.RecoveryTested, CustodyReason.AdministrativeAct);
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
