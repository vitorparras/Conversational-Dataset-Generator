namespace Core.Sample.Tests;

public sealed class MarkedOnTheMethodTests
{
    [Fact]
    [Trait("Category", "RealService")]
    public async Task StartsTheRealHost() => _ = await HostProcess.StartWithTheRealServiceAsync(default);

    [Fact]
    public async Task StartsTheFakeHost() => _ = await LoggedHost.StartAsync(realService: false, default);

    [Fact]
    public void KnowsNothingOfTheService() => Assert.Equal(4, 2 + 2);
}
