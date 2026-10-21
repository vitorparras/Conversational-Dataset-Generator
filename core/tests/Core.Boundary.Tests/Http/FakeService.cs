using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Core.Boundary.Tests.Http;

/// <summary>
/// A stand-in for the language service on a free loopback port, answering each route what the test gives it; a route
/// given no reply never answers. Version replies are used in order, the last one for every call after.
/// </summary>
internal sealed class FakeService : IDisposable
{
    private readonly HttpListener listener = new();
    private readonly CancellationTokenSource stop = new();
    private readonly ConcurrentQueue<SeenRequest> seen = new();
    private readonly IReadOnlyList<Reply> versions;
    private readonly Reply? analysis;
    private readonly Task serving;
    private int versionCalls;

    private FakeService(IReadOnlyList<Reply> versions, Reply? analysis)
    {
        this.versions = versions;
        this.analysis = analysis;
        Port = FreePort();
        listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        listener.Start();
        serving = ServeAsync();
    }

    public int Port { get; }

    public IReadOnlyList<SeenRequest> Seen => [.. seen];

    public static FakeService Answering(Reply analysis) => new([Reply.Version("1")], analysis);

    public static FakeService Answering(Reply version, Reply analysis) => new([version], analysis);

    public static FakeService WithVersions(IReadOnlyList<Reply> versions, Reply analysis) => new(versions, analysis);

    public static FakeService NeverAnalyzing() => new([Reply.Version("1")], analysis: null);

    /// <summary>A port nothing listens on: taken from the system and released at once.</summary>
    public static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    public void Dispose()
    {
        stop.Cancel();
        listener.Close();
        serving.GetAwaiter().GetResult();
        stop.Dispose();
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
        await context.Response.OutputStream.WriteAsync(bytes, stop.Token);
        context.Response.Close();
    }

    private Reply NextVersion() => versions[Math.Min(Interlocked.Increment(ref versionCalls), versions.Count) - 1];
}
