namespace Core.Host.Endpoints;

/// <summary>What the process says about itself when asked for its state.</summary>
internal sealed record StatusResponse(bool Alive);
