namespace Core.Persistence.Content;

/// <summary>The statements of the revelation of a marker (fatia D): a row that is only added, whether the item still holds its text, and its map, read only after the event.</summary>
public static partial class ContentQueries
{
    /// <summary>Writes the revelation by the authorization; the database lets it through only while the authorization holds, once, and with its event. No row when another took it.</summary>
    internal const string InsertReversalRevelation =
        "INSERT INTO content.reversal_revelation (domain_id, authorization_id) VALUES (@domain, @authorization) ON CONFLICT (authorization_id) DO NOTHING RETURNING id";

    /// <summary>True when the item still holds its text; false when it was emptied, and no row when the domain holds no such item.</summary>
    internal const string ReadItemHeld = "SELECT emptied_at IS NULL FROM content.item_text WHERE id = @item";

    /// <summary>The map of the item that still holds its entries, the newest; no row when there is none.</summary>
    internal const string ReadMapOfItem = "SELECT id FROM content.reversal_map WHERE item_id = @item AND emptied_at IS NULL ORDER BY id DESC LIMIT 1";
}
