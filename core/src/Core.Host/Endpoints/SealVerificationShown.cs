using Core.Persistence.Custody.Verification;

namespace Core.Host.Endpoints;

/// <summary>
/// The answer of verifying the chain against a seal: the links, told apart from the seal, and what that verification protects in the situation the installation declared.
/// It carries no content of any event, and a seal that does not hold is told here and in the log, and writes no failure of its own.
/// </summary>
/// <param name="Passed">Whether every link of the chain held.</param>
/// <param name="EventsRead">How many events the verification read.</param>
/// <param name="FirstInconsistent">The sequence of the first inconsistent event; null when the links held.</param>
/// <param name="Seal">How the chain stands against the seal.</param>
/// <param name="Situation">Where the installation declared it keeps the seal.</param>
/// <param name="Protection">What this verification protects against in that situation.</param>
internal sealed record SealVerificationShown(bool Passed, long EventsRead, long? FirstInconsistent, SealStanding Seal, string Situation, string Protection)
{
    internal static SealVerificationShown Of(ChainVerification verification, SealComparison seal, SealSituation situation) =>
        new(
            verification.Passed,
            verification.EventsRead,
            verification.FirstInconsistent?.Sequence,
            new SealStanding(seal.Holds, seal.Outcome.ToString(), seal.Statement, seal.Sealed.SealedAt, seal.Sealed.Events),
            situation.ToString(),
            SealProtection.Of(situation, seal.Sealed.SealedAt));
}
