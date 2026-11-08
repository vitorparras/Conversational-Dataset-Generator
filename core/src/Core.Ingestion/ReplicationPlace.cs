namespace Core.Ingestion;

/// <summary>Where a message is kept: the item that holds it and its position in the conversation, which the mark points to.</summary>
/// <param name="Item">The item that holds the message.</param>
/// <param name="Position">The position of the message in its conversation, from zero.</param>
public sealed record ReplicationPlace(long Item, int Position) : IComparable<ReplicationPlace>
{
    /// <summary>The order of the places: by item and then by position.</summary>
    /// <param name="other">The place to compare with.</param>
    public int CompareTo(ReplicationPlace? other) => other is null ? 1 : Item != other.Item ? Item.CompareTo(other.Item) : Position.CompareTo(other.Position);
}
