using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace Core.Host.Tests;

/// <summary>
/// A proxy on a loopback port that takes connections into its queue and never answers, so a client that goes through it
/// hangs until its own time limit; the queue tells afterwards whether anyone came.
/// </summary>
internal sealed class SilentProxy : IDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);

    public SilentProxy()
    {
        listener.Start();
        var address = string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}");
        Environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["HTTP_PROXY"] = address,
            ["HTTPS_PROXY"] = address,
            ["ALL_PROXY"] = address,
            ["http_proxy"] = address,
            ["https_proxy"] = address,
            ["all_proxy"] = address,
            ["NO_PROXY"] = string.Empty,
            ["no_proxy"] = string.Empty,
        };
    }

    /// <summary>Every proxy variable the platform and Python clients read, pointing here, and no host exempted.</summary>
    public IReadOnlyDictionary<string, string> Environment { get; }

    /// <summary>Whether a connection is waiting in the queue; nothing ever takes one out.</summary>
    public bool WasReached => listener.Pending();

    public void Dispose() => listener.Dispose();
}
