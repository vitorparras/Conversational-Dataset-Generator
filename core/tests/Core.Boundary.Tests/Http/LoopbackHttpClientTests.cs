using Core.Boundary.Http;

namespace Core.Boundary.Tests.Http;

/// <summary>The client the host talks to the service with leaves the time limit to its caller and never follows a redirect.</summary>
public sealed class LoopbackHttpClientTests
{
    [Fact]
    public void ClientLeavesTheTimeLimitToItsCaller()
    {
        using var client = LoopbackHttpClient.Create();

        Assert.Equal(Timeout.InfiniteTimeSpan, client.Timeout);
    }

    [Fact]
    public async Task RedirectIsNotFollowedSoTheSecretGoesNowhereElse()
    {
        using var recognition = new Recognition();
        using var elsewhere = FakeService.Answering(Reply.Fixture("accepted/response-empty.json"));
        var redirect = new Reply(307, "{}") { Location = $"http://127.0.0.1:{elsewhere.Port}/version" };
        using var service = FakeService.Answering(redirect, Reply.Fixture("accepted/response-empty.json"));

        _ = await recognition.AskAsync(service.Port);

        Assert.Empty(elsewhere.Seen);
    }
}
