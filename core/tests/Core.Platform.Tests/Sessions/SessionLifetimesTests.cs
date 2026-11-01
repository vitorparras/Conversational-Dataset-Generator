using Core.Platform.Sessions;

namespace Core.Platform.Tests.Sessions;

public sealed class SessionLifetimesTests
{
    [Fact]
    public void BothLimitsAreKeptAsGiven()
    {
        var lifetimes = new SessionLifetimes(TimeSpan.FromMinutes(30), TimeSpan.FromHours(8));

        Assert.Equal(TimeSpan.FromMinutes(30), lifetimes.Idle);
        Assert.Equal(TimeSpan.FromHours(8), lifetimes.Absolute);
    }

    [Theory]
    [InlineData(0, 8)]
    [InlineData(-1, 8)]
    [InlineData(30, 0)]
    [InlineData(30, -1)]
    public void ALimitThatIsNotAboveZeroIsRefused(int idleMinutes, int absoluteHours) =>
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => new SessionLifetimes(TimeSpan.FromMinutes(idleMinutes), TimeSpan.FromHours(absoluteHours)));
}
