namespace Core.Boundary.Http;

/// <summary>The client the core talks to the language service with, on this machine.</summary>
public static class LoopbackHttpClient
{
    /// <summary>
    /// A client that never goes through a proxy, never follows a redirect, which would send the secret on to wherever
    /// it points, and leaves the time limit to its caller, so the limit a caller configures is the one applied.
    /// </summary>
    public static HttpClient Create()
    {
        SocketsHttpHandler? handler = null;
        try
        {
            handler = new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false, ConnectCallback = ProvenConnection.OpenAsync };
            var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
            handler = null;
            return client;
        }
        finally
        {
            handler?.Dispose();
        }
    }
}
