using System.Net.Http.Json;
using System.Text.Json;

namespace Core.Tests.Integration.Host;

public sealed class StatusTests
{
    [Fact]
    public async Task RealProcessStartsAndAnswersStatus()
    {
        await using var host = HostProcess.Start([]);
        using var client = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5080") };

        var document = await PollStatusAsync(client, host);

        Assert.True(document.RootElement.GetProperty("alive").GetBoolean());
    }

    private static async Task<JsonDocument> PollStatusAsync(HttpClient client, HostProcess host)
    {
        var token = TestContext.Current.CancellationToken;
        for (var attempt = 0; attempt < 200; attempt++)
        {
            Assert.False(host.HasExited, host.Output);
            try
            {
                var body = await client.GetFromJsonAsync<JsonElement>("/status", token);
                return JsonDocument.Parse(body.GetRawText());
            }
            catch (HttpRequestException)
            {
                await Task.Delay(50, token);
            }
        }

        throw new TimeoutException("status did not answer: " + host.Output);
    }
}
