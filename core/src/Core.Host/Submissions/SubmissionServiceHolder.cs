using Core.Platform.Keys;

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
    internal SubmissionServiceHolder(long maxBytes, Func<CancellationToken, Task<Loaded>> build)
    {
        ArgumentNullException.ThrowIfNull(build);
        this.build = build;
        MaxBytes = maxBytes;
    }

    /// <summary>The most bytes a file may have.</summary>
    internal long MaxBytes { get; }

    /// <summary>A holder of a service already built, which holds no key of its own.</summary>
    /// <param name="service">The service.</param>
    /// <param name="maxBytes">The most bytes a file may have.</param>
    internal static SubmissionServiceHolder Of(SubmissionService service, long maxBytes) => new(maxBytes, _ => Task.FromResult(new Loaded(service, null)));

    /// <summary>The service, built by the first request that asks; one built with no key to seal with is not kept, so the next request tries again.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    internal async Task<Loaded> GetAsync(CancellationToken cancellationToken)
    {
        await turn.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var built = loaded ?? await build(cancellationToken).ConfigureAwait(false);
            loaded = built.Unsealed is null ? built : null;
            return built;
        }
        finally
        {
            _ = turn.Release();
        }
    }

    /// <summary>Lets go of the key the service holds.</summary>
    void IDisposable.Dispose()
    {
        loaded?.Key?.Dispose();
        turn.Dispose();
    }

    /// <summary>The service and what it holds that must be let go of with it.</summary>
    /// <param name="Service">The service.</param>
    /// <param name="Key">The key the sealing holds; null when the service holds none.</param>
    /// <param name="Unsealed">Why the key could not be read, when it could not: the service then has no key to seal with and only registers the submission as turned back.</param>
    internal sealed record Loaded(SubmissionService Service, IDisposable? Key, MasterKeyFailureKind? Unsealed = null);
}
