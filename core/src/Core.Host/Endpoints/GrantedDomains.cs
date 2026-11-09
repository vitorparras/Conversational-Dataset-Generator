using Core.Persistence.Domains;

namespace Core.Host.Endpoints;

/// <summary>From the names the grants and the selector of the header use to the identifiers the readings of the content take, in one place for every screen that needs them.</summary>
internal static class GrantedDomains
{
    /// <summary>The domains with the names given, by identifier, as the listing has them; a name no domain has is left out, and one that differs only in case is another name.</summary>
    /// <param name="listing">The listing of the domains.</param>
    /// <param name="names">The names, as the grants give them.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    internal static Task<IReadOnlyDictionary<int, string>> OfAsync(DomainListing listing, IEnumerable<string> names, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(listing);
        ArgumentNullException.ThrowIfNull(names);
        return FindingAsync(listing, names.ToHashSet(StringComparer.Ordinal), cancellationToken);
    }

    private static async Task<IReadOnlyDictionary<int, string>> FindingAsync(DomainListing listing, HashSet<string> names, CancellationToken cancellationToken)
    {
        var listed = await listing.ListAsync(cancellationToken).ConfigureAwait(false);
        return listed.Where(domain => names.Contains(domain.Name)).ToDictionary(domain => domain.Id, domain => domain.Name);
    }
}
