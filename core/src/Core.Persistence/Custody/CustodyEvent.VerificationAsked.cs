using Core.Persistence.Custody.Verification;

namespace Core.Persistence.Custody;

public sealed partial record CustodyEvent
{
    /// <summary>
    /// The event of a verification of the chain that someone asked for: the same verdict as <see cref="Verification(long, long?)" />, still written by the system as the chain requires of the step,
    /// with the id of whoever asked in the reference, so that the history of the verifications says who asked.
    /// </summary>
    /// <param name="eventsRead">How many events the verification read.</param>
    /// <param name="firstInconsistent">The sequence of the first inconsistent event; null when the chain held.</param>
    /// <param name="requestedBy">The id of whoever asked.</param>
    public static CustodyEvent Verification(long eventsRead, long? firstInconsistent, long requestedBy) =>
        Verification(eventsRead, firstInconsistent) with { Reference = RequesterReference.Of(requestedBy) };
}
