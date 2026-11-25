namespace Core.Ingestion.Pseudonymizing;

/// <summary>What the swap of one item gives: the pseudonymized text and the entries of the map of the item, in the order the markers first appear.</summary>
/// <param name="Text">The sanitized text with a marker in place of each span to pseudonymize.</param>
/// <param name="Entries">One entry for each marker and original the text holds.</param>
public sealed record Pseudonymized(string Text, IReadOnlyList<MapEntry> Entries)
{
    /// <summary>How many entries; never the text, which with the map identifies.</summary>
    public override string ToString() => $"Pseudonymized {{ Entries = {Entries.Count} }}";
}
