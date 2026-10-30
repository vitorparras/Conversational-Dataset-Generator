namespace Core.Sample.Tests;

[Trait("Category", "Database")]
public sealed class WrongMarkTests
{
    [Fact]
    [Trait("Kind", "RealService")]
    public async Task StartsTheRealHost() => _ = await HostProcess.StartWithTheRealServiceAsync(default);
}
