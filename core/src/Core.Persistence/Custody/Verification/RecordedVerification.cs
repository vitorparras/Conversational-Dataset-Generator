namespace Core.Persistence.Custody.Verification;

/// <summary>
/// The last verification of the chain as the chain itself records it: when, whether it held, how many events it read and, when it did not hold, the sequence of the first event that does not.
/// It carries no content of any event, and what the chain refused to record is not here.
/// </summary>
/// <param name="RecordedAt">The moment the server recorded the verdict.</param>
/// <param name="Passed">Whether every link held.</param>
/// <param name="EventsRead">How many events the verification read.</param>
/// <param name="FirstInconsistent">The sequence of the first inconsistent event; null when the chain held.</param>
/// <param name="RequestedBy">The id of whoever asked for it, when someone did; null for the verification the host runs by itself.</param>
/// <param name="RequestedByName">The name that id has now; null when nobody asked or the name is not found.</param>
public sealed record RecordedVerification(
    DateTimeOffset RecordedAt, bool Passed, long EventsRead, long? FirstInconsistent, long? RequestedBy = null, string? RequestedByName = null);
