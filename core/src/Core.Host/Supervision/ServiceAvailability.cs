using System.Collections.Frozen;

namespace Core.Host.Supervision;

/// <summary>The state of the language service; each change gives a new value, and a change the supervision never makes is a defect.</summary>
internal sealed class ServiceAvailability
{
    private static readonly FrozenSet<(ServiceState From, ServiceState To)> Allowed = new[]
    {
        (ServiceState.Starting, ServiceState.Available),
        (ServiceState.Starting, ServiceState.Unavailable),
        (ServiceState.Available, ServiceState.Unavailable),
        (ServiceState.Available, ServiceState.Restarting),
        (ServiceState.Unavailable, ServiceState.Restarting),
        (ServiceState.Restarting, ServiceState.Available),
        (ServiceState.Restarting, ServiceState.Unavailable),
    }.ToFrozenSet();

    private ServiceAvailability(ServiceState state)
    {
        State = state;
    }

    internal static ServiceAvailability Initial { get; } = new(ServiceState.Starting);

    internal ServiceState State { get; }

    /// <summary>The new state; stopping is allowed from anywhere, since the core can stop at any moment.</summary>
    internal ServiceAvailability MoveTo(ServiceState next) =>
        next == ServiceState.Stopped || Allowed.Contains((State, next))
            ? new ServiceAvailability(next)
            : throw new InvalidOperationException($"The language service cannot move from {State} to {next}.");
}
