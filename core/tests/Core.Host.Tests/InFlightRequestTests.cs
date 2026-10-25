namespace Core.Host.Tests;

/// <summary>A stop asked while a request is being served lets that request finish before the host ends.</summary>
public sealed class InFlightRequestTests
{
    [Fact]
    public async Task RequestInFlightWhenTheStopIsAskedIsAnsweredInFull()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await HeldRouteHost.StartAsync(cancellationToken);
        var answer = await host.HeldRequestAsync(cancellationToken);

        await host.AskToStopAsync(cancellationToken);
        await host.ReleaseAndWaitForStopAsync();

        Assert.Equal(HeldRouteHost.Answer, await answer);
    }
}
