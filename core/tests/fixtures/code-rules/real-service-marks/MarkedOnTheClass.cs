namespace Core.Sample.Tests;

[Trait("Category", "RealService")]
public sealed class MarkedOnTheClassTests
{
    [Fact]
    public async Task StartsTheRealHost() => _ = await HostProcess.StartWithTheRealServiceAsync(default);

    [Theory]
    [InlineData(1)]
    public void NeedsTheInterpreter(int unused) => _ = LanguageServiceInterpreter.Available;
}
