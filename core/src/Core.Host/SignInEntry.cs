namespace Core.Host;

/// <summary>
/// How an entry ended: the identifier of the session it opened; or no identifier because it was refused, whatever the reason; or no identifier because the chain stayed busy and the right
/// password could not be recorded, which is not a refusal of the credential and is told apart so that it is never answered as one.
/// </summary>
/// <param name="Identifier">The identifier of the session that was opened; null when it was not.</param>
/// <param name="ChainBusy">Whether the entry was left unrecorded by a chain that stayed busy, after the tries; a session was not opened, and the password was not judged wrong.</param>
internal readonly record struct SignInEntry(string? Identifier, bool ChainBusy)
{
    /// <summary>The entry that was refused.</summary>
    internal static SignInEntry Refused { get; } = new(null, false);

    /// <summary>The entry that the chain left unrecorded.</summary>
    internal static SignInEntry Busy { get; } = new(null, true);
}
