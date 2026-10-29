using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Core.Host.Tests.Analysis;

/// <summary>
/// Another process on a port a language service once held, doing what a service would: it answers the version route
/// with contract version 1 and any other with an empty analysis. It counts every connection it accepts, because the
/// proof it wants is that the core never connected at all, which also means nothing was sent to it.
/// </summary>
internal sealed class StrangerOnThePort : IDisposable
{
    private const string VersionBody = /*lang=json,strict*/ """{"service_version":"0.1.0","contract_version":"1","model_version":null}""";

    private readonly TcpListener listener;
    private readonly CancellationTokenSource stop = new();
    private readonly Task serving;
    private int connections;

    public StrangerOnThePort(int port)
    {
        listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        serving = ServeAsync();
    }

    /// <summary>How many connections the stranger accepted; the first thing it would have received is the secret.</summary>
    public int Connections => Volatile.Read(ref connections);

    public void Dispose()
    {
        stop.Cancel();
        listener.Dispose();
        serving.GetAwaiter().GetResult();
        stop.Dispose();
    }

    private async Task ServeAsync()
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                using var connection = await listener.AcceptTcpClientAsync(stop.Token);
                _ = Interlocked.Increment(ref connections);
                await AnswerAsync(connection.GetStream());
            }
            catch (Exception ended) when (ended is OperationCanceledException or SocketException or ObjectDisposedException or IOException)
            {
                return;
            }
        }
    }

    private async Task AnswerAsync(Stream stream)
    {
        var buffer = new byte[8192];
        var read = await stream.ReadAsync(buffer, stop.Token);
        var body = Encoding.ASCII.GetString(buffer, 0, read).StartsWith("GET /version", StringComparison.Ordinal) ? VersionBody : "{}";
        var answer = $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nConnection: close\r\nContent-Length: {body.Length}\r\n\r\n{body}";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(answer), stop.Token);
    }
}
