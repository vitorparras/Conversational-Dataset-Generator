namespace Core.Persistence.Tests.Fixtures;

/// <summary>
/// A synchronization context that counts the continuations posted to it and still runs them on the pool, so that code which
/// resumes on the context it started in is seen, and code which gives it up is not.
/// </summary>
internal sealed class CountingContext : SynchronizationContext
{
    private int posts;

    /// <summary>How many continuations were posted to this context.</summary>
    public int Posts => Volatile.Read(ref posts);

    /// <summary>Starts the work with this context current, and puts back the one that was current once the work returns its task.</summary>
    public Task<T> StartAsync<T>(Func<Task<T>> work)
    {
        var previous = Current;
        SetSynchronizationContext(this);
        try
        {
            return work();
        }
        finally
        {
            SetSynchronizationContext(previous);
        }
    }

    public override void Post(SendOrPostCallback d, object? state)
    {
        _ = Interlocked.Increment(ref posts);
        _ = ThreadPool.QueueUserWorkItem(_ => d(state));
    }
}
