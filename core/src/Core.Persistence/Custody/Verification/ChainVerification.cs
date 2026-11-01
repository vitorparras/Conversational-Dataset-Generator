namespace Core.Persistence.Custody.Verification;

/// <summary>
/// What a verification of the chain concluded and declared: what it covered, how many events it read, when, whether the chain held and, when it did not, the first event that does not.
/// It carries no content of any event.
/// </summary>
public sealed record ChainVerification
{
    /// <summary>What the verification covered.</summary>
    public required VerificationScope Scope { get; init; }

    /// <summary>How many events the verification read, in one consistent view of the chain.</summary>
    public required long EventsRead { get; init; }

    /// <summary>The moment the view was taken.</summary>
    public required DateTimeOffset VerifiedAt { get; init; }

    /// <summary>The first event that does not hold; null when the chain held.</summary>
    public InconsistentEvent? FirstInconsistent { get; init; }

    /// <summary>What the chain answered when the verdict was recorded as an event: appended with its sequence, or the reason it was not.</summary>
    public required CustodyResult Recorded { get; init; }

    /// <summary>Whether every link of the chain held.</summary>
    public bool Passed => FirstInconsistent is null;
}
