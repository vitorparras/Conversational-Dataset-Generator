using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Core.Host.Tests.Analysis;

/// <summary>
/// Another process on a port, listening the way a language service would, that writes down every byte each connection sends it, to the
/// end of the connection. What it answers to each chunk is the test's to say, so it can be a process that proves nothing, a process
/// that guesses the proof, or one that replays a proof it was once given.
/// </summary>
internal sealed class RecordingStranger : IDisposable
{
    private static readonly TimeSpan EndLimit = TimeSpan.FromSeconds(30);

    private readonly TcpListener listener;
    private readonly CancellationTokenSource stop = new();
    private readonly Func<int, int, string, string?> answer;
    private readonly ConcurrentDictionary<int, ConcurrentQueue<string>> received = new();
    private readonly ConcurrentBag<Task> connections = [];
    private readonly Task serving;
    private int accepted;

    /// <param name="port">The port to hold; 0 for any free one.</param>
    /// <param name="answer">
    /// The connection (from 0, in the order accepted), the chunk of it (from 0) and the text received in that chunk;
    /// returns the whole HTTP answer to write, or null to write nothing.
    /// </param>
    public RecordingStranger(int port, Func<int, int, string, string?> answer)
    {
        this.answer = answer;
        listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        serving = ServeAsync();
    }

    public int Port { get; }

    /// <summary>How many connections the stranger accepted.</summary>
    public int Connections => Volatile.Read(ref accepted);

    /// <summary>Everything the connection sent, once the connection has ended; empty for a connection that sent nothing.</summary>
    public string ReceivedOn(int connection) => received.TryGetValue(connection, out var chunks) ? string.Concat(chunks) : string.Empty;

    /// <summary>Everything every connection sent.</summary>
    public string ReceivedAll => string.Concat(Enumerable.Range(0, Connections).Select(ReceivedOn));

    /// <summary>The nonce each connection asked the proof of, in the order accepted; a connection that asked none is not among them.</summary>
    public IReadOnlyList<string> Nonces => [.. Enumerable.Range(0, Connections).Select(ReceivedOn).Select(Nonce).OfType<string>()];

    /// <summary>The nonce a request carries; null when it carries none.</summary>
    public static string? Nonce(string request)
    {
        const string Header = "x-service-nonce: ";
        var start = request.IndexOf(Header, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }

        start += Header.Length;
        var end = request.IndexOf('\r', start);
        return request[start..(end < 0 ? request.Length : end)];
    }

    /// <summary>Waits until every connection accepted so far has ended, which is when what it sent is all written down.</summary>
    public Task AllEndedAsync() => Task.WhenAll(connections).WaitAsync(EndLimit, TestContext.Current.CancellationToken);

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
                var connection = await listener.AcceptTcpClientAsync(stop.Token);
                var index = Interlocked.Increment(ref accepted) - 1;
                connections.Add(Task.Run(() => ListenAsync(index, connection), CancellationToken.None));
            }
            catch (Exception ended) when (ended is OperationCanceledException or SocketException or ObjectDisposedException)
            {
                return;
            }
        }
    }

    private async Task ListenAsync(int index, TcpClient connection)
    {
        using (connection)
        {
            var chunks = received.GetOrAdd(index, _ => new ConcurrentQueue<string>());
            var stream = connection.GetStream();
            var buffer = new byte[8192];
            try
            {
                var chunk = 0;
                int read;
                while ((read = await stream.ReadAsync(buffer, stop.Token)) > 0)
                {
                    var text = Encoding.UTF8.GetString(buffer, 0, read);
                    chunks.Enqueue(text);
                    if (answer(index, chunk++, text) is { } reply)
                    {
                        await stream.WriteAsync(Encoding.ASCII.GetBytes(reply), stop.Token);
                    }
                }
            }
            catch (Exception ended) when (ended is OperationCanceledException or SocketException or ObjectDisposedException or IOException)
            {
                return;
            }
        }
    }
}
