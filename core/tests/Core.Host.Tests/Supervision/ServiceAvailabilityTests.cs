using Core.Host.Supervision;

namespace Core.Host.Tests.Supervision;

/// <summary>The state machine of the language service allows only the moves the supervision makes; any other is a defect.</summary>
public sealed class ServiceAvailabilityTests
{
    [Fact]
    public void ServiceStartsInTheStartingState()
    {
        var availability = ServiceAvailability.Initial;

        Assert.Equal(ServiceState.Starting, availability.State);
    }

    [Theory]
    [InlineData(nameof(ServiceState.Starting), nameof(ServiceState.Available))]
    [InlineData(nameof(ServiceState.Starting), nameof(ServiceState.Unavailable))]
    [InlineData(nameof(ServiceState.Available), nameof(ServiceState.Unavailable))]
    [InlineData(nameof(ServiceState.Available), nameof(ServiceState.Restarting))]
    [InlineData(nameof(ServiceState.Unavailable), nameof(ServiceState.Restarting))]
    [InlineData(nameof(ServiceState.Restarting), nameof(ServiceState.Available))]
    [InlineData(nameof(ServiceState.Restarting), nameof(ServiceState.Unavailable))]
    [InlineData(nameof(ServiceState.Starting), nameof(ServiceState.Stopped))]
    [InlineData(nameof(ServiceState.Available), nameof(ServiceState.Stopped))]
    [InlineData(nameof(ServiceState.Unavailable), nameof(ServiceState.Stopped))]
    [InlineData(nameof(ServiceState.Restarting), nameof(ServiceState.Stopped))]
    [InlineData(nameof(ServiceState.Stopped), nameof(ServiceState.Stopped))]
    public void MoveTheSupervisionMakesReachesTheNewState(string from, string to)
    {
        var availability = Reach(Enum.Parse<ServiceState>(from));

        var moved = availability.MoveTo(Enum.Parse<ServiceState>(to));

        Assert.Equal(to, moved.State.ToString());
    }

    [Theory]
    [InlineData(nameof(ServiceState.Starting), nameof(ServiceState.Starting))]
    [InlineData(nameof(ServiceState.Starting), nameof(ServiceState.Restarting))]
    [InlineData(nameof(ServiceState.Available), nameof(ServiceState.Available))]
    [InlineData(nameof(ServiceState.Available), nameof(ServiceState.Starting))]
    [InlineData(nameof(ServiceState.Unavailable), nameof(ServiceState.Available))]
    [InlineData(nameof(ServiceState.Unavailable), nameof(ServiceState.Unavailable))]
    [InlineData(nameof(ServiceState.Restarting), nameof(ServiceState.Restarting))]
    [InlineData(nameof(ServiceState.Stopped), nameof(ServiceState.Available))]
    [InlineData(nameof(ServiceState.Stopped), nameof(ServiceState.Restarting))]
    [InlineData(nameof(ServiceState.Unavailable), nameof(ServiceState.Starting))]
    [InlineData(nameof(ServiceState.Restarting), nameof(ServiceState.Starting))]
    [InlineData(nameof(ServiceState.Stopped), nameof(ServiceState.Starting))]
    [InlineData(nameof(ServiceState.Stopped), nameof(ServiceState.Unavailable))]
    public void MoveTheSupervisionNeverMakesIsADefect(string from, string to)
    {
        var availability = Reach(Enum.Parse<ServiceState>(from));

        _ = Assert.Throws<InvalidOperationException>(() => availability.MoveTo(Enum.Parse<ServiceState>(to)));
    }

    [Fact]
    public void MovingLeavesThePreviousValueAsItWas()
    {
        var availability = ServiceAvailability.Initial;

        _ = availability.MoveTo(ServiceState.Available);

        Assert.Equal(ServiceState.Starting, availability.State);
    }

    /// <summary>The value in the given state, reached by moves the supervision makes.</summary>
    private static ServiceAvailability Reach(ServiceState state) => state switch
    {
        ServiceState.Starting => ServiceAvailability.Initial,
        ServiceState.Available or ServiceState.Unavailable or ServiceState.Stopped => ServiceAvailability.Initial.MoveTo(state),
        ServiceState.Restarting => Reach(ServiceState.Available).MoveTo(ServiceState.Restarting),
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };
}
