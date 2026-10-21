using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Core.Boundary.Tests.Http;

/// <summary>A listener on a free loopback port that answers every connection with the same bytes and closes it, whatever they are.</summary>
internal sealed class RawService : IDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stop = new();
    private readonly byte[] answer;
    private readonly Task serving;

    public RawService(string answer)
    {
        this.answer = Encoding.ASCII.GetBytes(answer);
        listener.Start();
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        serving = ServeAsync();
    }

    public int Port { get; }

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
                var stream = connection.GetStream();
                _ = await stream.ReadAsync(new byte[4096], stop.Token);
                await stream.WriteAsync(answer, stop.Token);
            }
            catch (Exception ended) when (ended is OperationCanceledException or SocketException or ObjectDisposedException or IOException)
            {
                return;
            }
        }
    }
}
