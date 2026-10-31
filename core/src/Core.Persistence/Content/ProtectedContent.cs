using System.Security.Cryptography;
using System.Text;
using Core.Platform.Keys;

namespace Core.Persistence.Content;

/// <summary>
/// The one way protected content reaches the database and comes back: it is sealed under the label <c>&lt;purpose&gt;/&lt;domain&gt;/v&lt;n&gt;</c>
/// of the newest generation the installation loaded, and opened under the label its envelope carries, only if that label is the purpose of
/// the column and the domain of the row. Anything stored in a protected column that this path did not seal is refused when read.
/// </summary>
public sealed class ProtectedContent
{
    private readonly EnvelopeCipher cipher;
    private readonly DerivationLabels loaded;

    /// <summary>The path seals with the cipher, under the labels the installation loaded with its master key.</summary>
    public ProtectedContent(EnvelopeCipher cipher, DerivationLabels loaded)
    {
        ArgumentNullException.ThrowIfNull(cipher);
        ArgumentNullException.ThrowIfNull(loaded);
        this.cipher = cipher;
        this.loaded = loaded;
    }

    /// <summary>The text sealed for the column of the domain; a domain with no label loaded for the purpose is a defect of whoever created it.</summary>
    public SealedContent Seal(ProtectedColumn column, int domainId, string text)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentNullException.ThrowIfNull(text);
        var label = NewestLabel(column.Purpose, domainId);
        var bytes = Encoding.UTF8.GetBytes(text);
        try
        {
            return new SealedContent(column, domainId, cipher.Seal(label.Text, bytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    /// <summary>The text of a value read from the column of the domain; a value that is not an envelope of this purpose and domain is refused, never read as empty.</summary>
    public string Open(ProtectedColumn column, int domainId, ReadOnlySpan<byte> stored)
    {
        ArgumentNullException.ThrowIfNull(column);
        var label = LabelOf(stored);
        if (label.Purpose != column.Purpose || label.DomainId != domainId)
        {
            throw new EnvelopeRefusedException($"The envelope was not sealed for the purpose and the domain of {column.Table}.{column.Name}.");
        }

        var bytes = cipher.Open(label.Text, stored);
        try
        {
            return Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static DerivationLabel LabelOf(ReadOnlySpan<byte> stored) =>
        DerivationLabel.TryParse(EnvelopeCipher.LabelOf(stored), out var label)
            ? label
            : throw new EnvelopeRefusedException("The label in the header is not a derivation label.");

    private DerivationLabel NewestLabel(LabelPurpose purpose, int domainId) =>
        loaded.All
            .Select(text => DerivationLabel.TryParse(text, out var label) ? label : null)
            .OfType<DerivationLabel>()
            .Where(label => label.Purpose == purpose && label.DomainId == domainId)
            .MaxBy(label => label.Version)
        ?? throw new InvalidOperationException($"No derivation label is loaded for the purpose {purpose} and the domain {domainId}.");
}
