using System.Text;
using Core.Platform.Keys;

namespace Core.Persistence.Content;

/// <summary>
/// The one way protected content reaches the database and comes back: each row has a key of its own, drawn for it and wrapped under the label <c>&lt;purpose&gt;/&lt;domain&gt;/v&lt;n&gt;</c>
/// of the newest generation the installation loaded and under the epoch of its custody file, and what the row holds sealed is sealed by that key. It is opened only if the labels the
/// envelopes carry are the purpose of the column and the domain of the row. Anything stored in a protected column that this path did not seal is refused when read.
/// </summary>
public sealed class ProtectedContent
{
    private readonly DerivationLabels loaded;
    private readonly ItemKeys keys;
    private readonly IReadOnlySet<string> revoked;

    /// <summary>The path draws the keys of the rows with <paramref name="keys"/>, under the labels the installation loaded with its master key, none of them revoked.</summary>
    public ProtectedContent(DerivationLabels loaded, ItemKeys keys)
        : this(loaded, keys, new HashSet<string>(StringComparer.Ordinal))
    {
    }

    /// <summary>As above, except that a label in <paramref name="revoked"/> seals nothing new: the newest generation not revoked is used, and what it sealed is opened all the same.</summary>
    public ProtectedContent(DerivationLabels loaded, ItemKeys keys, IReadOnlySet<string> revoked)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(revoked);
        this.loaded = loaded;
        this.keys = keys;
        this.revoked = revoked;
    }

    /// <summary>The key of a new row of the domain, for the purpose of the column; a domain with no label loaded for the purpose is a defect of the caller.</summary>
    public ItemSealing Begin(ProtectedColumn column, int domainId)
    {
        ArgumentNullException.ThrowIfNull(column);
        var label = NewestLabel(column.Purpose, domainId);
        return new ItemSealing(keys.Create(label.Text), label);
    }

    /// <summary>
    /// The text of a value read from the column of the domain, with the key its row keeps; a value or a key that is not of this purpose and domain,
    /// or does not authenticate, is refused, never read as empty.
    /// </summary>
    public string Open(ProtectedColumn column, int domainId, ReadOnlySpan<byte> stored, ContentKey key)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentNullException.ThrowIfNull(key);
        var label = Checked(column, domainId, stored);
        _ = Checked(column, domainId, key.Wrapped.Span);
        using var item = keys.Unwrap(key.Epoch, key.Wrapped.Span);
        return Encoding.UTF8.GetString(EnvelopeCipher.OpenWith(item.AsSpan(), label.Text, stored));
    }

    private DerivationLabel Checked(ProtectedColumn column, int domainId, ReadOnlySpan<byte> bytes)
    {
        var label = LabelOf(bytes);
        if (label.Purpose != column.Purpose || label.DomainId != domainId)
        {
            throw new EnvelopeRefusedException($"The envelope was not sealed for the purpose and the domain of {column.Table}.{column.Name}.");
        }

        return loaded.Holds(label.Text) ? label : throw new EnvelopeRefusedException($"The label '{label.Text}' of the envelope is not among the loaded ones.");
    }

    private static DerivationLabel LabelOf(ReadOnlySpan<byte> stored) =>
        DerivationLabel.TryParse(EnvelopeCipher.LabelOf(stored), out var label)
            ? label
            : throw new EnvelopeRefusedException("The label in the header is not a derivation label.");

    private DerivationLabel NewestLabel(LabelPurpose purpose, int domainId) =>
        loaded.All
            .Select(text => DerivationLabel.TryParse(text, out var label) ? label : null)
            .OfType<DerivationLabel>()
            .Where(label => label.Purpose == purpose && label.DomainId == domainId && !revoked.Contains(label.Text))
            .MaxBy(label => label.Version)
        ?? throw new InvalidOperationException($"No derivation label is loaded for the purpose {purpose} and the domain {domainId}.");
}
