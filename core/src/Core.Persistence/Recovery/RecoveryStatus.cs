namespace Core.Persistence.Recovery;

/// <summary>Whether the recovery was rehearsed and recorded, and what is missing when it was not.</summary>
/// <param name="Tested">Whether the step of the rehearsal is in the chain.</param>
/// <param name="Missing">What is missing, in words; null when the rehearsal is recorded.</param>
public sealed record RecoveryStatus(bool Tested, string? Missing);
