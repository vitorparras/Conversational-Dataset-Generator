using Core.Host.Screens;
using Core.Persistence.Domains;
using Core.Persistence.Ingestion;
using Core.Persistence.People;

namespace Core.Host.Endpoints;

/// <summary>
/// What the screen of the batches (#608) reads for one page: the domains granted by their identifiers, the batches of those domains alone from the register of the submissions,
/// and the names of who submitted, read once for the page. Nothing of the material is read.
/// </summary>
internal static class BatchListing
{
    private const string Removed = "Pessoa removida";

    /// <summary>The last page the address may ask for: past it the register would skip more than it reads for each domain, so the page stops there and says there is no next.</summary>
    internal static int LastPage { get; } = BatchRegister.MostSkipped / BatchesScreen.Size;

    /// <summary>The rows of the page asked, whether any domain is granted, and whether a next page exists; one more batch than the page holds is read to know it.</summary>
    /// <param name="sources">The register, the listing of the domains and the registry of the people.</param>
    /// <param name="granted">The names of the domains granted to the one who entered.</param>
    /// <param name="filter">The state, the period and the page, as the address asks for them; the page runs from one up to <see cref="LastPage"/>.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    internal static async Task<(List<BatchesScreen.Row> Rows, bool Granted, bool HasNext)> PageAsync(
        (BatchRegister Register, DomainListing Listing, PersonRegistry Registry) sources,
        IEnumerable<string> granted,
        (BatchState? State, DateTimeOffset? From, DateTimeOffset? Until, int Page) filter,
        CancellationToken cancellationToken)
    {
        var rows = new List<BatchesScreen.Row>();
        var domains = await GrantedDomains.OfAsync(sources.Listing, granted, cancellationToken).ConfigureAwait(false);
        if (domains.Count == 0)
        {
            return (rows, false, false);
        }

        var asked = new BatchFilter
        {
            Domains = domains.Keys,
            State = filter.State,
            From = filter.From,
            Until = filter.Until,
            Skip = (filter.Page - 1) * BatchesScreen.Size,
            Take = BatchesScreen.Size + 1,
        };
        var found = await sources.Register.ListAsync(asked, cancellationToken).ConfigureAwait(false);
        var names = await sources.Registry.NamesOfAsync(found.Select(entry => entry.SubmittedBy), cancellationToken).ConfigureAwait(false);
        rows.AddRange(found.Take(BatchesScreen.Size).Select(entry => RowOf(entry, domains[entry.DomainId], names)));
        return (rows, true, found.Count > BatchesScreen.Size && filter.Page < LastPage);
    }

    /// <summary>One batch as the list shows it: the domain and the person by name, a person the registry no longer has said as removed and never by the identifier.</summary>
    private static BatchesScreen.Row RowOf(BatchEntry entry, string domain, IReadOnlyDictionary<long, string> names) => new(
        entry.Id,
        domain,
        entry.State,
        entry.Reason,
        names.TryGetValue(entry.SubmittedBy, out var name) ? name : Removed,
        entry.SubmittedAt,
        entry.Receipt is { } receipt ? new BatchesScreen.Receipt(receipt.Bytes, receipt.Digest, receipt.Warning) : null);
}
