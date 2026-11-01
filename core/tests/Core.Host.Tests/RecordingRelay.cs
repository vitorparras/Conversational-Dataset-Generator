using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Core.Host.Tests;

/// <summary>A relay between a client and the host on the loopback that keeps every byte that crosses it in both directions, as an observer of the network would.</summary>
internal sealed class RecordingRelay : IDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly ConcurrentQueue<byte[]> seen = new();
    private readonly Task serving;

    public RecordingRelay(int hostPort)
    {
        listener.Start();
        serving = ServeAsync(hostPort);
    }

    /// <summary>The port the client connects to instead of the host's.</summary>
    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

    /// <summary>Stops listening and hands over everything that crossed the relay until now.</summary>
    public async Task<byte[]> StopAsync()
    {
        listener.Stop();
        _ = await Task.WhenAny(serving, Task.Delay(TimeSpan.FromSeconds(2)));
        return [.. seen.SelectMany(chunk => chunk)];
    }

    public void Dispose() => listener.Dispose();

    private async Task ServeAsync(int hostPort)
    {
        try
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var upstream = new TcpClient();
            await upstream.ConnectAsync(IPAddress.Loopback, hostPort);
            _ = await Task.WhenAny(PumpAsync(client.GetStream(), upstream.GetStream()), PumpAsync(upstream.GetStream(), client.GetStream()));
        }
        catch (Exception exception) when (exception is IOException or SocketException or ObjectDisposedException or InvalidOperationException)
        {
            // The client or the host closed the connection, which is how the relay is meant to end.
        }
    }

    private async Task PumpAsync(Stream from, Stream to)
    {
        var buffer = new byte[4096];
        int count;
        while ((count = await from.ReadAsync(buffer)) > 0)
        {
            seen.Enqueue(buffer[..count]);
            await to.WriteAsync(buffer.AsMemory(0, count));
        }
    }
}
