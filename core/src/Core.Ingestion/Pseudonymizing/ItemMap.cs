namespace Core.Ingestion.Pseudonymizing;

/// <summary>The map of one item, opened to pseudonymize the item again with the markers it already has.</summary>
/// <param name="ItemId">The item the map belongs to.</param>
/// <param name="Entries">The entries of the map.</param>
public sealed record ItemMap(long ItemId, IReadOnlyList<MapEntry> Entries)
{
    /// <summary>The item and how many entries; never an entry.</summary>
    public override string ToString() => $"ItemMap {{ ItemId = {ItemId}, Entries = {Entries.Count} }}";
}
