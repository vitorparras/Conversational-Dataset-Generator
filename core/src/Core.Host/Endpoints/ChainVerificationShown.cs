using Core.Persistence.Custody.Verification;

namespace Core.Host.Endpoints;

/// <summary>
/// The answer about the verification of the chain: the last one the chain recorded (null when it holds none), the interval in force and whether a verification is late.
/// The last conference of the guarantees of the server is shown with it.
/// It carries no content of any event.
/// </summary>
internal sealed record ChainVerificationShown(RecordedVerification? Last, TimeSpan Interval, bool Overdue, GuaranteesShown Guarantees);
