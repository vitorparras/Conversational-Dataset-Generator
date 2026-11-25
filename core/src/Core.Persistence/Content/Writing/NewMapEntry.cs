namespace Core.Persistence.Content.Writing;

/// <summary>An entry of the map of an item as it is when the item is added: the marker, what it replaced and the destination decided for the occurrence; the original is sealed.</summary>
/// <param name="Marker">The marker written in the pseudonymized text.</param>
/// <param name="Original">What the marker replaced; sealed with the key of the map and never stored in clear.</param>
/// <param name="Author">Whether it stands for an author of the conversation.</param>
/// <param name="Destination">What was decided for the occurrence; only an occurrence to pseudonymize becomes an entry, and any other refuses the batch.</param>
public sealed record NewMapEntry(string Marker, string Original, bool Author, OccurrenceDestination Destination)
{
    /// <summary>The marker and the destination; never the original.</summary>
    public override string ToString() => $"NewMapEntry {{ Marker = {Marker}, Author = {Author}, Destination = {Destination} }}";
}
