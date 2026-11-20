namespace Core.Persistence.Content.Rerun;

/// <summary>The request of a person to rerun the sanitization of a domain to a version of the list of steps.</summary>
/// <param name="DomainId">The domain whose items of accepted batches are queued.</param>
/// <param name="PersonId">Who asks; the chain records them.</param>
/// <param name="Version">The version the items are rerun to: the SHA-256, in lowercase hexadecimal, of <paramref name="Steps"/>.</param>
/// <param name="Steps">The steps of the version, one name@version per line, in the order they run.</param>
public sealed record RerunRequest(int DomainId, long PersonId, string Version, string Steps);
