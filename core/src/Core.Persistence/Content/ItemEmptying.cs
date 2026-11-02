using Core.Persistence.Custody;

namespace Core.Persistence.Content;

/// <summary>The request to empty an item: who asks, which item of which domain, why, and the protocol outside the system that asked, by number and by nothing else.</summary>
/// <param name="Author">Who empties; the chain records them, and only a person who holds the function of curation of the collection may.</param>
/// <param name="DomainId">The domain the item belongs to; an item of another domain is not found.</param>
/// <param name="ItemId">The identifier of the item in <c>content.item_text</c>.</param>
/// <param name="Motive">Why the emptying is asked for.</param>
/// <param name="Reference">The number of the protocol outside the system: letters, digits and . _ / - only, up to 64 characters; no name and no data of whoever asked.</param>
public sealed record ItemEmptying(CustodyAuthor Author, int DomainId, long ItemId, EmptyingMotive Motive, string Reference);
