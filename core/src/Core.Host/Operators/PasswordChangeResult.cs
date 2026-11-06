using Core.Persistence.People;

namespace Core.Host.Operators;

/// <summary>The end of a change of the own password, with how many other sessions it ended and, when it was not written, what the registry said.</summary>
/// <param name="Outcome">How it ended.</param>
/// <param name="SessionsEnded">The other sessions of the account that the change ended; none unless it was changed.</param>
/// <param name="Failure">The answer of the registry when the change was not written; null otherwise.</param>
internal sealed record PasswordChangeResult(PasswordChangeOutcome Outcome, int SessionsEnded = 0, PersonResult? Failure = null);
