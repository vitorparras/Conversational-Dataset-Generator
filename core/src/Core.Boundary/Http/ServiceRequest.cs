namespace Core.Boundary.Http;

/// <summary>How a request carries the startup secret to the language service: in its header, and in its options, where the connection that opens for it finds the secret to demand proof of.</summary>
public static class ServiceRequest
{
    internal const string SecretHeader = "x-service-secret";

    internal static readonly HttpRequestOptionsKey<string> SecretOption = new("Core.Boundary.Http.Secret");

    /// <summary>Adds the secret to the request; the client sends it only over a connection whose other end has proved to hold it.</summary>
    /// <param name="request">The request to the language service.</param>
    /// <param name="secret">The secret the service was started with.</param>
    public static void AddSecret(this HttpRequestMessage request, string secret)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Headers.Add(SecretHeader, secret);
        request.Options.Set(SecretOption, secret);
    }
}
