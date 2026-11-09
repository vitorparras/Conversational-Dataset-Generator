namespace Core.Ingestion;

/// <summary>Where a message is kept: the item that holds it and its position in the conversation, which the mark points to.</summary>
/// <param name="Item">The item that holds the message.</param>
/// <param name="Position">The position of the message in its conversation, from zero.</param>
public sealed record ReplicationPlace(long Item, int Position) : IComparable<ReplicationPlace>
{
    /// <summary>Whether a place comes before another: by item and then by position.</summary>
    /// <param name="left">The first place.</param>
    /// <param name="right">The second place.</param>
    public static bool operator <(ReplicationPlace? left, ReplicationPlace? right) => left is null ? right is not null : left.CompareTo(right) < 0;

    /// <summary>Whether a place comes before another or is the same.</summary>
    /// <param name="left">The first place.</param>
    /// <param name="right">The second place.</param>
    public static bool operator <=(ReplicationPlace? left, ReplicationPlace? right) => left is null || left.CompareTo(right) <= 0;

    /// <summary>Whether a place comes after another.</summary>
    /// <param name="left">The first place.</param>
    /// <param name="right">The second place.</param>
    public static bool operator >(ReplicationPlace? left, ReplicationPlace? right) => left is not null && left.CompareTo(right) > 0;

    /// <summary>Whether a place comes after another or is the same.</summary>
    /// <param name="left">The first place.</param>
    /// <param name="right">The second place.</param>
    public static bool operator >=(ReplicationPlace? left, ReplicationPlace? right) => left is null ? right is null : left.CompareTo(right) >= 0;

    /// <summary>The order of the places: by item and then by position.</summary>
    /// <param name="other">The place to compare with.</param>
    public int CompareTo(ReplicationPlace? other)
    {
        if (other is null)
        {
            return 1;
        }

        var byItem = Item.CompareTo(other.Item);
        return byItem != 0 ? byItem : Position.CompareTo(other.Position);
    }
}
