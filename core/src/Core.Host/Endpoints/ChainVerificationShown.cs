using Core.Persistence.Custody.Verification;

namespace Core.Host.Endpoints;

/// <summary>
/// The answer about the verification of the chain: the last one the chain recorded (null when it holds none), the interval in force, whether a verification is late, where the installation
/// declared it keeps the seal, what a verification against it protects and the last conference of the guarantees. It carries no content of any event.
/// </summary>
/// <param name="Last">The last verification the chain recorded; null when it holds none.</param>
/// <param name="Interval">The interval in force.</param>
/// <param name="Overdue">Whether a verification is late.</param>
/// <param name="Situation">Where the installation declared it keeps the seal.</param>
/// <param name="Protection">What a verification against the seal protects in that situation, in words that never claim integrity without qualifying it.</param>
/// <param name="LastSeal">When the chain last recorded a seal; null when it holds none.</param>
/// <param name="Warning">The warning of the manual situation, when no seal was ever recorded or the last is older than seven days; null otherwise.</param>
/// <param name="Guarantees">The last conference of the guarantees of the server, as the chain recorded it.</param>
internal sealed record ChainVerificationShown(
    RecordedVerification? Last, TimeSpan Interval, bool Overdue, string Situation, string Protection, DateTimeOffset? LastSeal, string? Warning, GuaranteesShown Guarantees);
