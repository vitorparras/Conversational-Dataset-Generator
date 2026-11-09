using Core.Persistence.Content.Emptying;
using Core.Persistence.Custody;
using Core.Persistence.Domains;
using Npgsql;

namespace Core.Host.Endpoints;

/// <summary>
/// What the screens of the emptying (#617) ask of the server, in one place: the identifier of a domain by its name, the record of an item that was emptied and the emptying itself, with the person
/// of the session, the motive and the protocol. The emptying is the one of <see cref="ContentEmptier"/>; this class decides nothing about who may empty or which item is found. It reads no text.
/// </summary>
internal sealed class ItemEmptyingDesk
{
    private readonly DomainListing domains;
    private readonly EmptiedItemReader records;
    private readonly ContentEmptier emptier;

    private ItemEmptyingDesk(NpgsqlDataSource source)
    {
        domains = new DomainListing(source);
        records = new EmptiedItemReader(source);
        emptier = new ContentEmptier(source);
    }

    /// <summary>The desk over the data source built with the application's credential.</summary>
    /// <param name="source">The source of the connections.</param>
    internal static ItemEmptyingDesk On(NpgsqlDataSource source) => new(source);

    /// <summary>The identifier of the domain with the name given, as the active domain of the person names it; none when no domain has the name.</summary>
    /// <param name="name">The name of the domain.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    internal async Task<int?> DomainIdOfAsync(string name, CancellationToken cancellationToken)
    {
        var listed = await domains.ListAsync(cancellationToken).ConfigureAwait(false);
        return listed.FirstOrDefault(domain => string.Equals(domain.Name, name, StringComparison.Ordinal))?.Id;
    }

    /// <summary>The record of the emptying of the item; none when the item was not emptied, is not in the domain or does not exist: the record does not tell them apart.</summary>
    /// <param name="domainId">The domain the item is looked for in.</param>
    /// <param name="itemId">The identifier of the item.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    internal Task<EmptiedItem?> RecordOfAsync(int domainId, long itemId, CancellationToken cancellationToken) => records.ReadAsync(domainId, itemId, cancellationToken);

    /// <summary>Empties the item in the name of the person of the session, with the motive and the protocol of the request; the answer says how it ended.</summary>
    /// <param name="entered">The identifier of the one who entered, the person of the session; never one a request says.</param>
    /// <param name="request">The item, the domain, the motive and the protocol.</param>
    /// <param name="cancellationToken">Cancels the emptying.</param>
    internal Task<EmptyingResult> EmptyAsync(long entered, EmptyingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return emptier.EmptyAsync(new ItemEmptying(CustodyAuthor.Person(entered), request.DomainId, request.ItemId, request.Motive, request.Reference), cancellationToken);
    }

    /// <summary>What the confirmation of the emptying sends: the item of the domain, why, and the protocol that asked.</summary>
    /// <param name="DomainId">The domain of the item.</param>
    /// <param name="ItemId">The identifier of the item.</param>
    /// <param name="Motive">Why the emptying is asked for.</param>
    /// <param name="Reference">The protocol number; the emptier refuses what is not one.</param>
    internal sealed record EmptyingRequest(int DomainId, long ItemId, EmptyingMotive Motive, string Reference);
}
