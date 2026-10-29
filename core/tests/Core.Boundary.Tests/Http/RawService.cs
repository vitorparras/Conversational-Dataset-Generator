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
    private readonly ProofMode proof;
    private readonly Func<string, string>? firstAnswer;
    private readonly Task serving;

    /// <param name="answer">What every connection is answered after the proof.</param>
    /// <param name="proof">How the proof is answered.</param>
    /// <param name="firstAnswer">The whole answer to the proof question, given the nonce, instead of the proof the mode gives.</param>
    public RawService(string answer, ProofMode proof = ProofMode.Right, Func<string, string>? firstAnswer = null)
    {
        this.proof = proof;
        this.firstAnswer = firstAnswer;
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

    /// <summary>Answers the core's first question, the proof, the way the service does; what comes after is the raw answer.</summary>
    private async Task AnswerProofAsync(Stream stream)
    {
        var buffer = new byte[4096];
        var read = await stream.ReadAsync(buffer, stop.Token);
        if (TestProof.NonceIn(Encoding.ASCII.GetString(buffer, 0, read)) is { } nonce)
        {
            await stream.WriteAsync(firstAnswer is null ? TestProof.Answer(proof, nonce) : Encoding.ASCII.GetBytes(firstAnswer(nonce)), stop.Token);
        }
    }

    private async Task ServeAsync()
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                using var connection = await listener.AcceptTcpClientAsync(stop.Token);
                var stream = connection.GetStream();
                await AnswerProofAsync(stream);
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
