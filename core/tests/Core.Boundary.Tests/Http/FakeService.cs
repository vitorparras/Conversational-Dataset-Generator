using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Core.Boundary.Tests.Http;

/// <summary>
/// A stand-in for the language service on a loopback port it holds from the moment it is chosen, answering each route
/// what the test gives it; a route given no reply never answers. Version replies are used in order, the last one for
/// every call after.
/// </summary>
internal sealed class FakeService : IDisposable
{
    private const int FirstPort = 49152;
    private const int LastPort = 65535;
    private const int Attempts = 200;

    private readonly HttpListener listener;
    private readonly CancellationTokenSource stop = new();
    private readonly ConcurrentQueue<SeenRequest> seen = new();
    private readonly IReadOnlyList<Reply> versions;
    private readonly Reply? analysis;
    private readonly Task serving;
    private int versionCalls;
    private readonly ConcurrentQueue<string> nonces = new();
    private readonly ConcurrentQueue<string?> proofSecrets = new();

    private FakeService(IReadOnlyList<Reply> versions, Reply? analysis)
    {
        this.versions = versions;
        this.analysis = analysis;
        (listener, var port) = Listen();
        Port = port;
        serving = ServeAsync();
    }

    public int Port { get; }

    public IReadOnlyList<SeenRequest> Seen => [.. seen];

    /// <summary>What the service does when asked to prove it holds the secret; set before the first request.</summary>
    public ProofMode Proof { get; set; } = ProofMode.Right;

    /// <summary>The nonce of each proof asked of the service, in order; such a request is not among <see cref="Seen"/>.</summary>
    public IReadOnlyList<string> Nonces => [.. nonces];

    /// <summary>The secret header each proof request carried, null when it carried none, which is what it must do.</summary>
    public IReadOnlyList<string?> ProofSecrets => [.. proofSecrets];

    public static FakeService Answering(Reply analysis) => new([Reply.Version("1")], analysis);

    public static FakeService Answering(Reply version, Reply analysis) => new([version], analysis);

    public static FakeService WithVersions(IReadOnlyList<Reply> versions, Reply analysis) => new(versions, analysis);

    public static FakeService NeverAnalyzing() => new([Reply.Version("1")], analysis: null);

    public void Dispose()
    {
        stop.Cancel();
        listener.Close();
        serving.GetAwaiter().GetResult();
        stop.Dispose();
    }

    /// <summary>
    /// A listener started on a port no one else holds. The listener cannot be given port 0, so the start itself is the
    /// check: a port taken by another process fails it and the next one is tried; there is no moment in which the port
    /// was chosen but not yet held.
    /// </summary>
    private static (HttpListener Listener, int Port) Listen()
    {
        HttpListenerException? taken = null;
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var port = RandomNumberGenerator.GetInt32(FirstPort, LastPort + 1);
            var candidate = new HttpListener();
            candidate.Prefixes.Add($"http://127.0.0.1:{port}/");
            try
            {
                candidate.Start();
                return (candidate, port);
            }
            catch (HttpListenerException refused)
            {
                taken = refused;
                candidate.Close();
            }
        }

        throw new InvalidOperationException($"No loopback port could be held in {Attempts} attempts.", taken);
    }

    private async Task ServeAsync()
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                var context = await listener.GetContextAsync().WaitAsync(stop.Token);
                _ = Task.Run(() => AnswerAsync(context), CancellationToken.None);
            }
            catch (Exception ended) when (ended is OperationCanceledException or HttpListenerException or ObjectDisposedException)
            {
                return;
            }
        }
    }

    private async Task AnswerAsync(HttpListenerContext context)
    {
        if (context.Request.Headers[TestProof.NonceHeader] is { } nonce)
        {
            await ProveAsync(context, nonce);
            return;
        }

        using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(stop.Token);
        var path = context.Request.Url!.AbsolutePath;
        seen.Enqueue(new SeenRequest(context.Request.HttpMethod, path, context.Request.Headers["x-service-secret"], body));
        var reply = path == "/version" ? NextVersion() : analysis;
        if (reply is null)
        {
            await Task.Delay(Timeout.Infinite, stop.Token).ContinueWith(_ => context.Response.Abort(), TaskScheduler.Default);
            return;
        }

        await Task.Delay(reply.Delay, stop.Token);
        var bytes = Encoding.UTF8.GetBytes(reply.Body);
        context.Response.StatusCode = reply.Status;
        context.Response.RedirectLocation = reply.Location;
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes, stop.Token);
        context.Response.Close();
    }

    private async Task ProveAsync(HttpListenerContext context, string nonce)
    {
        nonces.Enqueue(nonce);
        proofSecrets.Enqueue(context.Request.Headers["x-service-secret"]);
        if (TestProof.Given(Proof, nonce) is { } proof)
        {
            context.Response.Headers[TestProof.ProofHeader] = proof;
        }

        var bytes = "{}"u8.ToArray();
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes, stop.Token);
        context.Response.Close();
    }

    private Reply NextVersion() => versions[Math.Min(Interlocked.Increment(ref versionCalls), versions.Count) - 1];
}
