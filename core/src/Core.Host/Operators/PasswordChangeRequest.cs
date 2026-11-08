using System.Net;

namespace Core.Host.Operators;

/// <summary>Who asks to change their own password: the account of the session, the identifier of that session so that it stays, and where the request came from.</summary>
/// <param name="Account">The account of the session that asks.</param>
/// <param name="Session">The identifier of the session that asks, which the change does not end.</param>
/// <param name="Origin">The address the request came from, of which only the prefix is counted.</param>
internal sealed record PasswordChangeRequest(long Account, string? Session, IPAddress? Origin);
