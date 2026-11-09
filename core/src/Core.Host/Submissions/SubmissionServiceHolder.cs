namespace Core.Host.Submissions;

/// <summary>
/// The way to the service of the submissions: sealing an origin needs the master key, the epochs and the labels registered and revoked.
/// Each use reads them again and lets the reading go when it is done, so that a domain created after the host started, a label revoked or an epoch rotated is seen by the next submission and no key outlives the use that needed it.
/// </summary>
internal sealed class SubmissionServiceHolder
{
    private readonly Func<CancellationToken, Task<Loaded?>> build;

    /// <summary>The holder over the way to build the service.</summary>
    /// <param name="maxBytes">The most bytes a file may have, which the route bounds the body by.</param>
    /// <param name="build">Reads the key and the labels and builds the service over them; null when the key cannot be read.</param>
    internal SubmissionServiceHolder(long maxBytes, Func<CancellationToken, Task<Loaded?>> build)
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
    internal static SubmissionServiceHolder Of(SubmissionService service, long maxBytes) => new(maxBytes, _ => Task.FromResult<Loaded?>(new Loaded(service, null)));

    /// <summary>The service over a reading of the key and the labels made now, which whoever asks lets go of when done; null when the key cannot be read, and then nothing can be sealed.</summary>
    /// <param name="cancellationToken">Cancels the reading.</param>
    internal Task<Loaded?> GetAsync(CancellationToken cancellationToken) => build(cancellationToken);

    /// <summary>The service and what it holds that must be let go of with it.</summary>
    /// <param name="Service">The service.</param>
    /// <param name="Key">The key the sealing holds; null when the service holds none.</param>
    internal sealed record Loaded(SubmissionService Service, IDisposable? Key) : IDisposable
    {
        /// <summary>Lets go of the key.</summary>
        public void Dispose() => Key?.Dispose();
    }
}
