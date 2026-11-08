namespace Core.Host.Endpoints;

/// <summary>The answer to a password changed: how many other sessions of the person it ended.</summary>
/// <param name="SessionsEnded">The other sessions that are over.</param>
internal sealed record PasswordChanged(int SessionsEnded);
