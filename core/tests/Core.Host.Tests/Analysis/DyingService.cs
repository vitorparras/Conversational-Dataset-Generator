using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Core.Host.Tests.Analysis;

/// <summary>
/// A stand-in for the language service on a free loopback port that declares contract version 1 and then, asked for an
/// analysis, ends the connection without a word, as a service that dies with the request in flight does.
/// </summary>
internal sealed class DyingService : IDisposable
{
    private const string VersionBody = /*lang=json,strict*/ """{"service_version":"0.1.0","contract_version":"1","model_version":null}""";
    private const string VersionHead = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nConnection: close\r\n";

    private static readonly string VersionAnswer =
        string.Create(CultureInfo.InvariantCulture, $"{VersionHead}Content-Length: {VersionBody.Length}\r\n\r\n{VersionBody}");

    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stop = new();
    private readonly Task serving;

    public DyingService()
    {
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
                await AnswerAsync(connection.GetStream());
            }
            catch (Exception ended) when (ended is OperationCanceledException or SocketException or ObjectDisposedException or IOException)
            {
                return;
            }
        }
    }

    /// <summary>The version route is answered; any other request is read and the connection closed with nothing written.</summary>
    private async Task AnswerAsync(Stream stream)
    {
        var buffer = new byte[8192];
        var read = await stream.ReadAsync(buffer, stop.Token);
        var request = Encoding.ASCII.GetString(buffer, 0, read);
        var answer = request.StartsWith("GET /version", StringComparison.Ordinal) ? VersionAnswer : string.Empty;
        await stream.WriteAsync(Encoding.ASCII.GetBytes(answer), stop.Token);
    }
}
