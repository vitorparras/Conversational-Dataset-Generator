namespace Core.Host.Endpoints;

/// <summary>Metadata of a route that only a person with a live session reaches, with the route where the entry is made, to which a request without one is pointed.</summary>
internal sealed record SessionRequired(string SignIn);
