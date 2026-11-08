namespace Core.Host.Operators;

/// <summary>How the change of the own password of a person ended.</summary>
internal enum PasswordChangeOutcome
{
    /// <summary>The new password is the one that opens, the event is in the chain and the other sessions are over.</summary>
    Changed,

    /// <summary>The password given as the current one was not accepted, or the limits of the entry were full; the same for every cause, and the refusal counted in the delay of the entry.</summary>
    Refused,

    /// <summary>The new password is empty; nothing was checked and nothing was counted.</summary>
    EmptyReplacement,

    /// <summary>The current password was right but the change was not written, and the registry says why.</summary>
    NotWritten,
}
