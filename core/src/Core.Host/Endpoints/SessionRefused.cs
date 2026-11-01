namespace Core.Host.Endpoints;

/// <summary>The answer to a request that has no live session or whose entry was refused, with the route where the entry is made; the same for every cause.</summary>
internal sealed record SessionRefused(string SignIn);
