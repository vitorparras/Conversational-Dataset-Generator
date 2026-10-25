namespace Core.Host.Supervision;

/// <summary>A request to restart the language service: who asked, and an opaque reference that ties the request to its outcome in the log.</summary>
internal sealed record RestartRequest(RestartRequester Requester, Guid Reference);
