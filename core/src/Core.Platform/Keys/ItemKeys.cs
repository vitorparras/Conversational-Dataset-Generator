using System.Security.Cryptography;
using System.Text;

namespace Core.Platform.Keys;

/// <summary>
/// Draws the key of each row of protected content and wraps it (decision 0049). The wrapping key is derived from the key of the label, which carries the domain and the purpose, and from the
/// secret of an epoch that lives in a custody file and not in the database: <c>HKDF(key of the label, salt = secret of the epoch, info = "kek/&lt;epoch&gt;")</c>. A row keeps its key
/// wrapped, with the number of the epoch; with the epoch gone, the key cannot be unwrapped by the master key and the labels alone, which is what makes an emptied row stay so.
/// </summary>
public sealed class ItemKeys
{
    private readonly KeyDerivation derivation;
    private readonly CustodyEpochs epochs;

    /// <summary>The keys of the rows are wrapped under the labels the derivation holds and the epochs given.</summary>
    public ItemKeys(KeyDerivation derivation, CustodyEpochs epochs)
    {
        ArgumentNullException.ThrowIfNull(derivation);
        ArgumentNullException.ThrowIfNull(epochs);
        this.derivation = derivation;
        this.epochs = epochs;
    }

    /// <summary>A new key, wrapped under the label and the newest epoch; with no epoch loaded there is nothing to wrap it under.</summary>
    public NewItemKey Create(string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        var epoch = epochs.Newest;
        var key = new ItemKey(RandomNumberGenerator.GetBytes(ItemKey.Length));
        try
        {
            return new NewItemKey(key, epoch, Wrap(key, label, epoch));
        }
        catch
        {
            key.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The key a row kept, from its wrapped bytes and the epoch it names; a label or an epoch not loaded, or bytes that do not authenticate under them,
    /// are refused with <see cref="EnvelopeRefusedException"/>.
    /// </summary>
    public ItemKey Unwrap(int epoch, ReadOnlySpan<byte> wrapped)
    {
        var label = EnvelopeCipher.LabelOf(wrapped);
        if (!epochs.Holds(epoch))
        {
            throw new EnvelopeRefusedException($"The epoch {epoch} of the key is not among the loaded ones.");
        }

        var kek = Kek(label, epoch);
        try
        {
            var opened = EnvelopeCipher.OpenWith(kek, label, wrapped);
            try
            {
                return new ItemKey(opened);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(opened);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    private byte[] Wrap(ItemKey key, string label, int epoch)
    {
        var kek = Kek(label, epoch);
        try
        {
            return EnvelopeCipher.SealWith(kek, label, key.AsSpan());
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    private byte[] Kek(string label, int epoch)
    {
        var kek = GC.AllocateArray<byte>(ItemKey.Length, pinned: true);
        using var labelKey = derivation.Derive(label);
        Hkdf.Derive(labelKey.AsSpan(), epochs.SecretOf(epoch), Encoding.UTF8.GetBytes($"kek/{epoch}"), kek);
        return kek;
    }
}
