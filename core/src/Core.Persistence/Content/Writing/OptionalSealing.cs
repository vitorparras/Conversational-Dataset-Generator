using Core.Persistence.Content.Sealing;

namespace Core.Persistence.Content.Writing;

/// <summary>The texts an item may lack, sealed through one condition instead of one copy of it for each text.</summary>
internal static class OptionalSealing
{
    /// <summary>The text sealed for the column of the row, or nothing for a text the item does not have.</summary>
    internal static SealedContent? SealIfAny(this ItemSealing sealing, ProtectedColumn column, string? text) => text is null ? null : sealing.Seal(column, text);
}
