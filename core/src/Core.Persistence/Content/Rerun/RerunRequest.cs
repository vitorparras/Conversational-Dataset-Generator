namespace Core.Persistence.Content.Rerun;

/// <summary>The request of a person to rerun the items of a domain to a version of the steps, one name@version per line.</summary>
/// <param name="DomainId">The domain.</param>
/// <param name="PersonId">Who asks.</param>
/// <param name="Version">The SHA-256, in lowercase hexadecimal, of the steps.</param>
/// <param name="Steps">The steps.</param>
public readonly record struct RerunRequest(int DomainId, long PersonId, string Version, string Steps);
