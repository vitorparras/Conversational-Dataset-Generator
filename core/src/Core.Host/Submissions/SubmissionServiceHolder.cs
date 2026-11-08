namespace Core.Host.Submissions;

/// <summary>
/// The service of the submissions, put together on first use because sealing an origin needs the master key, which the host reads when it is needed and holds until it stops.
/// A failure to read is not kept: the next request tries again.
/// </summary>
internal sealed class SubmissionServiceHolder : IDisposable
{
    private readonly SemaphoreSlim turn = new(1, 1);
    private readonly Func<CancellationToken, Task<Loaded>> build;
    private Loaded? loaded;

    /// <summary>The holder over the way to build the service.</summary>
    /// <param name="maxBytes">The most bytes a file may have, which the route bounds the body by.</param>
    /// <param name="build">Builds the service and the key it holds.</param>
    public SubmissionServiceHolder(long maxBytes, Func<CancellationToken, Task<Loaded>> build)
    {
        ArgumentNullException.ThrowIfNull(build);
        this.build = build;
        MaxBytes = maxBytes;
    }

    /// <summary>The most bytes a file may have.</summary>
    public long MaxBytes { get; }

    /// <summary>A holder of a service already built, which holds no key of its own.</summary>
    /// <param name="service">The service.</param>
    /// <param name="maxBytes">The most bytes a file may have.</param>
    internal static SubmissionServiceHolder Of(SubmissionService service, long maxBytes) => new(maxBytes, _ => Task.FromResult(new Loaded(service, null)));

    /// <summary>The service, built by the first request that asks.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    public async Task<SubmissionService> GetAsync(CancellationToken cancellationToken)
    {
        await turn.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            loaded ??= await build(cancellationToken).ConfigureAwait(false);
            return loaded.Service;
        }
        finally
        {
            _ = turn.Release();
        }
    }

    /// <summary>Lets go of the key the service holds.</summary>
    public void Dispose()
    {
        loaded?.Key?.Dispose();
        turn.Dispose();
    }

    /// <summary>The service and what it holds that must be let go of with it.</summary>
    /// <param name="Service">The service.</param>
    /// <param name="Key">The key the sealing holds; null when the service holds none.</param>
    internal sealed record Loaded(SubmissionService Service, IDisposable? Key);
}
