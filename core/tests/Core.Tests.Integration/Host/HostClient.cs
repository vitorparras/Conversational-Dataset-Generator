using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace Core.Tests.Integration.Host;

/// <summary>Talks to a host process over its loopback port.</summary>
internal sealed class HostClient : IDisposable
{
    // The host answers within seconds; waiting longer only delays the report of a failure.
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(30);

    // Windows takes about 2 s to report a refused loopback connection: without a connect timeout,
    // each attempt costs that much and the wait is counted in attempts, not in time.
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromMilliseconds(250);

    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(50);

    private readonly SocketsHttpHandler handler = new() { ConnectTimeout = ConnectTimeout };
    private readonly HttpClient client;
    private readonly HostProcess host;

    public HostClient(HostProcess host, int port)
    {
        this.host = host;
        client = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}")),
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    /// <summary>
    /// Polls until the route answers, for at most <see cref="Deadline"/> in all; fails with the host
    /// output if the process exits first, and says how long it waited if it never answers.
    /// </summary>
    public async Task<JsonElement> GetWhenUpAsync(string route)
    {
        var token = TestContext.Current.CancellationToken;
        var waited = Stopwatch.StartNew();
        var attempts = 0;
        while (waited.Elapsed < Deadline)
        {
            Assert.False(host.HasExited, "host exited:\n" + host.Output);
            attempts++;
            using var attempt = CancellationTokenSource.CreateLinkedTokenSource(token);
            attempt.CancelAfter(Deadline - waited.Elapsed);
            try
            {
                return await client.GetFromJsonAsync<JsonElement>(route, attempt.Token);
            }
            catch (HttpRequestException)
            {
                // Refused: the port is not open yet.
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                // The connect timeout, or the deadline reached in the middle of a request.
            }

            await Task.Delay(Interval, token);
        }

        throw new TimeoutException(string.Create(
            CultureInfo.InvariantCulture,
            $"{route} did not answer after waiting {waited.Elapsed.TotalSeconds:F1} s in {attempts} attempts:\n{host.Output}"));
    }

    public void Dispose()
    {
        client.Dispose();
        handler.Dispose();
    }
}
