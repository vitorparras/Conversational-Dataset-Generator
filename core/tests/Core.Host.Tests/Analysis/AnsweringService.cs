using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Core.Host.Tests.Supervision;

namespace Core.Host.Tests.Analysis;

/// <summary>
/// A stand-in for the language service on a free loopback port that proves it holds the secret, declares contract version 1 and answers every analysis with the body given,
/// whatever the text asked: a service whose answer the boundary has to judge.
/// </summary>
internal sealed partial class AnsweringService : IDisposable
{
    private const string VersionBody = /*lang=json,strict*/ """{"service_version":"0.1.0","contract_version":"1","model_version":null}""";

    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stop = new();
    private readonly string analysisBody;
    private readonly Task serving;

    public AnsweringService(string analysisBody)
    {
        this.analysisBody = analysisBody;
        listener.Start();
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        serving = ServeAsync();
    }

    public int Port { get; }

    /// <summary>The secret the stand-in proves it holds; the recognizer under test is given the same one.</summary>
    public string Secret { get; } = SupervisionHarness.NewSecret();

    public void Dispose()
    {
        stop.Cancel();
        listener.Dispose();
        serving.GetAwaiter().GetResult();
        stop.Dispose();
    }

    private static string Answer(string body) =>
        string.Create(CultureInfo.InvariantCulture, $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nConnection: close\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\n\r\n{body}");

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

    /// <summary>The proof is given first, as the service does; then the version route is answered with the version, and any other request with the analysis given.</summary>
    private async Task AnswerAsync(Stream stream)
    {
        var request = await ReadRequestAsync(stream);
        if (ProofAnswer.To(request, Secret) is { } proof)
        {
            await stream.WriteAsync(Encoding.ASCII.GetBytes(proof), stop.Token);
            request = await ReadRequestAsync(stream);
        }

        var answer = Answer(request.StartsWith("GET /version", StringComparison.Ordinal) ? VersionBody : analysisBody);
        await stream.WriteAsync(Encoding.UTF8.GetBytes(answer), stop.Token);
    }

    /// <summary>The head and the whole body the head announces, so that the connection is never closed with a part of the request unread.</summary>
    private async Task<string> ReadRequestAsync(Stream stream)
    {
        var received = new List<byte>();
        var buffer = new byte[8192];
        var read = -1;
        while (read != 0 && !Complete([.. received]))
        {
            read = await stream.ReadAsync(buffer, stop.Token);
            received.AddRange(buffer.AsSpan(0, read));
        }

        return Encoding.UTF8.GetString([.. received]);
    }

    private static bool Complete(byte[] received)
    {
        var text = Encoding.UTF8.GetString(received);
        var end = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        if (end < 0)
        {
            return false;
        }

        var length = ContentLength().Match(text[..end]);
        var body = length.Success ? int.Parse(length.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
        return received.Length >= Encoding.UTF8.GetByteCount(text[..end]) + 4 + body;
    }

    [GeneratedRegex("^content-length: ([0-9]+)\r?$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex ContentLength();
}
