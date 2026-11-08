namespace Core.Host.Endpoints;

/// <summary>The answer to an entry whose credential was not refused but that could not be recorded now: the chain was busy, and the entry may be sent again.</summary>
internal sealed record SessionUnavailable(string Reason);
