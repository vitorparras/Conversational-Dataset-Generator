using System.Text;
using Core.Persistence.Content.Sealing;
using Core.Platform.Keys;

namespace Core.Persistence.Content;

/// <summary>
/// The key of one row, drawn for it and wrapped, and what seals under it: every protected column of the row is sealed by the key of the row, whose purpose and domain the label of its
/// wrapping says. The row stores the wrapped key beside the content, and the key itself is zeroed when this is released.
/// </summary>
public sealed class ItemSealing : IDisposable
{
    internal ItemSealing()
    {
    }

    internal NewItemKey Created { get; init; } = null!;

    internal DerivationLabel Label { get; init; } = null!;

    /// <summary>The key to store with the row.</summary>
    public ContentKey Stored => new(Created.Epoch, Created.Wrapped);

    /// <summary>The text sealed for the column of the row; a column whose purpose is not the one the key was drawn for is refused.</summary>
    public SealedContent Seal(ProtectedColumn column, string text)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentNullException.ThrowIfNull(text);
        if (column.Purpose != Label.Purpose)
        {
            throw new ArgumentException("The key of the row was not drawn for the purpose of the column.", nameof(column));
        }

        return new SealedContent(column, Label.DomainId, EnvelopeCipher.SealWith(Created.Key.AsSpan(), Label.Text, Encoding.UTF8.GetBytes(text)));
    }

    /// <summary>Releases the key.</summary>
    public void Dispose() => Created.Dispose();
}
