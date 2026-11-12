using System.Net;

namespace Core.Host;

/// <summary>What an entry brings: the name and the password, where it came from, and the browser's own description of itself, which only becomes a short description of the device.</summary>
/// <param name="Name">The name asked for.</param>
/// <param name="Password">The password offered.</param>
/// <param name="Origin">The network address it came from; null when it is unknown.</param>
/// <param name="UserAgent">The <c>User-Agent</c> header; null when the request brought none.</param>
internal readonly record struct SignInRequest(string Name, string Password, IPAddress? Origin, string? UserAgent = null);
