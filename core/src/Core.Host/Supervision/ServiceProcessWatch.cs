namespace Core.Host.Supervision;

/// <summary>
/// Holds the state of the language service and watches its process, so the end of the process is seen
/// when it happens rather than at the next request; every change of state goes through one lock here.
/// </summary>
internal sealed class ServiceProcessWatch
{
    private readonly Lock gate = new();
    private ServiceAvailability availability = ServiceAvailability.Initial;
    private RunningService? watched;

    internal ServiceState State
    {
        get
        {
            lock (gate)
            {
                return availability.State;
            }
        }
    }

    /// <summary>Moves to a state other than available; the end of the process watched until now no longer counts as a loss.</summary>
    internal void Move(ServiceState next)
    {
        if (next == ServiceState.Available)
        {
            throw new InvalidOperationException("Only a watched process makes the language service available.");
        }

        lock (gate)
        {
            availability = availability.MoveTo(next);
            watched = null;
        }
    }

    /// <summary>
    /// Makes the service available and, from now on, takes the end of its process as the loss of the service;
    /// the report of the loss runs under the lock while the state still reads available, so it must not touch this watch.
    /// </summary>
    internal void Watch(RunningService service, Action<RunningService> lost)
    {
        lock (gate)
        {
            availability = availability.MoveTo(ServiceState.Available);
            watched = service;
        }

        _ = ObserveAsync(service, lost);
    }

    /// <summary>
    /// Reports the loss and then moves to unavailable, both under the lock, so the process the report reads
    /// cannot be released in between; the move happens even when the report throws, whose exception the
    /// discarded task then drops, since a dead process must never read as available.
    /// </summary>
    private async Task ObserveAsync(RunningService service, Action<RunningService> lost)
    {
        await service.WaitForExitAsync();
        lock (gate)
        {
            if (!ReferenceEquals(watched, service))
            {
                return;
            }

            watched = null;
            try
            {
                lost(service);
            }
            finally
            {
                availability = availability.MoveTo(ServiceState.Unavailable);
            }
        }
    }
}
