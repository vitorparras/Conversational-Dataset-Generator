using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace Core.Tests.Integration.Host;

/// <summary>Talks to a host process over its loopback port.</summary>
internal sealed class HostClient : IDisposable
{
    private readonly HttpClient client;
    private readonly HostProcess host;

    public HostClient(HostProcess host, int port)
    {
        this.host = host;
        client = new HttpClient
        {
            BaseAddress = new Uri(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}")),
        };
    }

    /// <summary>Polls until the route answers; fails with the host output if the process exits first.</summary>
    public async Task<JsonElement> GetWhenUpAsync(string route)
    {
        var token = TestContext.Current.CancellationToken;
        for (var attempt = 0; attempt < 300; attempt++)
        {
            Assert.False(host.HasExited, "host exited:\n" + host.Output);
            try
            {
                return await client.GetFromJsonAsync<JsonElement>(route, token);
            }
            catch (HttpRequestException)
            {
                await Task.Delay(50, token);
            }
        }

        throw new TimeoutException($"{route} did not answer:\n{host.Output}");
    }

    public void Dispose() => client.Dispose();
}
