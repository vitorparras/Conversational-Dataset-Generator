namespace Core.Persistence.Custody.Verification;

/// <summary>
/// The last conference of the guarantees as the chain itself records it: when, whether every guarantee held and, when not, the mask of the ones missing.
/// It carries no content of any event.
/// </summary>
/// <param name="RecordedAt">The moment the server recorded the verdict.</param>
/// <param name="Passed">Whether every guarantee held.</param>
/// <param name="MissingMask">The bits of the guarantees missing, as the closed list of the conference declares them; zero when none is.</param>
public sealed record RecordedGuarantees(DateTimeOffset RecordedAt, bool Passed, long MissingMask);
