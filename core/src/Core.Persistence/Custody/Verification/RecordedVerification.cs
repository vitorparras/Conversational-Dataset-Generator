namespace Core.Persistence.Custody.Verification;

/// <summary>
/// The last verification of the chain as the chain itself records it: when, whether it held, how many events it read and, when it did not hold, the sequence of the first event that does not.
/// It carries no content of any event, and what the chain refused to record is not here.
/// </summary>
/// <param name="RecordedAt">The moment the server recorded the verdict.</param>
/// <param name="Passed">Whether every link held.</param>
/// <param name="EventsRead">How many events the verification read.</param>
/// <param name="FirstInconsistent">The sequence of the first inconsistent event; null when the chain held.</param>
public sealed record RecordedVerification(DateTimeOffset RecordedAt, bool Passed, long EventsRead, long? FirstInconsistent);
