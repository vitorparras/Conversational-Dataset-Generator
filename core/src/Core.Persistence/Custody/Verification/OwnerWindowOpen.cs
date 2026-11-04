namespace Core.Persistence.Custody.Verification;

/// <summary>A window of the owner credential still open, as the chain records it (when it opened, the versions it spans, the tip then, and whether its closing failed); no content.</summary>
public sealed record OwnerWindowOpen(long OpenedSequence, DateTimeOffset OpenedAt, int From, int To, long TipSequence, bool AfterTheStep, bool ClosingFailed);
