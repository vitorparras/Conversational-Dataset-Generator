namespace Core.Host.Tests.Supervision;

/// <summary>
/// Processes a test expects to end by themselves; on dispose, the ones still running are killed, so a failure never leaves one behind.
/// Each is known by its number and start time, so a number another process took over is never killed.
/// </summary>
internal sealed class Survivors : IDisposable
{
    private readonly IReadOnlyList<ProcessIdentity> identities;

    /// <summary>The processes with these numbers now; one that has already ended is not read, there is nothing left to kill.</summary>
    public Survivors(IReadOnlyList<int> ids)
        : this([.. ids.Select(ProcessIdentity.Of).OfType<ProcessIdentity>()])
    {
    }

    public Survivors(IReadOnlyList<ProcessIdentity> identities)
    {
        this.identities = identities;
    }

    public void Dispose()
    {
        foreach (var identity in identities.Where(ProcessTree.IsRunning))
        {
            _ = identity.Kill();
        }
    }
}
