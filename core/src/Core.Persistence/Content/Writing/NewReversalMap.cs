namespace Core.Persistence.Content.Writing;

/// <summary>The map that reverses the pseudonyms of an item, as it is when it is added: which item, the batch the item is in, and what each pseudonym replaced.</summary>
/// <param name="BatchId">The batch of the item, which must be open; the database refuses a map whose batch is not the item's.</param>
/// <param name="ItemId">The item the map belongs to; an item of another domain is refused by the database.</param>
/// <param name="Entries">What each pseudonym replaced; sealed on its way to the database.</param>
public sealed record NewReversalMap(long BatchId, long ItemId, string Entries);
