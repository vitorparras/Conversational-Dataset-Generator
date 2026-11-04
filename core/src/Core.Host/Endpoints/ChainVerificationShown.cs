using Core.Persistence.Custody.Verification;

namespace Core.Host.Endpoints;

/// <summary>
/// The answer about the verification of the chain: the last one the chain recorded (null when it holds none), the interval in force, whether a verification is late, where the installation
/// declared it keeps the seal and what a verification against it protects. It carries no content of any event.
/// </summary>
/// <param name="Last">The last verification the chain recorded; null when it holds none.</param>
/// <param name="Interval">The interval in force.</param>
/// <param name="Overdue">Whether a verification is late.</param>
/// <param name="Situation">Where the installation declared it keeps the seal.</param>
/// <param name="Protection">What a verification against the seal protects in that situation, in words that never claim integrity without qualifying it.</param>
/// <param name="LastSeal">When the chain last recorded a seal; null when it holds none.</param>
internal sealed record ChainVerificationShown(RecordedVerification? Last, TimeSpan Interval, bool Overdue, string Situation, string Protection, DateTimeOffset? LastSeal);
