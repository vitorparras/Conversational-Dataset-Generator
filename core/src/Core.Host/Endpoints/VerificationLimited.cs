namespace Core.Host.Endpoints;

/// <summary>Why a verification against the seal was refused with 429 before the chain was read: the session already asked within the window, and how long is left of it.</summary>
/// <param name="Reason">The reason in words, for whoever operates.</param>
/// <param name="RetryAfterSeconds">The seconds to wait, the same as the <c>Retry-After</c> header.</param>
internal sealed record VerificationLimited(string Reason, int RetryAfterSeconds);
